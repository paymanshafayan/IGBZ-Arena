using IGBZ.Application.Abstractions;
using IGBZ.Domain.Catalog;
using IGBZ.Domain.Ordering;
using MongoDB.Driver;

namespace IGBZ.Infrastructure.Mongo;

/// <summary>
/// پیاده‌سازی سرویس تسویه‌حساب با تراکنش تک‌مرحله‌ای مونوگودی‌بی (گام ۲).
/// رزرو موجودی و ثبت سفارش را در یک تراکنش واحد انجام می‌دهد و در صورت شکست تراکنش را لغو می‌کند.
/// </summary>
public sealed class MongoCheckoutService(
    IMongoDatabase database,
    ITenantContext tenantContext,
    string productsCollectionName = MongoCollections.Products,
    string ordersCollectionName = MongoCollections.Orders) : ICheckoutService
{
    private readonly IMongoCollection<Product> _products = database.GetCollection<Product>(productsCollectionName);
    private readonly IMongoCollection<Order> _orders = database.GetCollection<Order>(ordersCollectionName);
    private readonly IMongoClient _client = database.Client;

    public async Task<CheckoutResult> CheckoutAsync(
        Order order,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        var tenantId = tenantContext.Current.Value;
        if (!string.Equals(order.TenantId, tenantId, StringComparison.Ordinal))
        {
            throw new CrossTenantAccessException(
                $"Order belongs to tenant '{order.TenantId}' but the current tenant is '{tenantId}'.");
        }

        using var session = await _client.StartSessionAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        session.StartTransaction();
        try
        {
            var shortages = new List<InventoryShortage>();

            foreach (var line in order.Lines)
            {
                var filter = Builders<Product>.Filter.And(
                    Builders<Product>.Filter.Eq("_id", line.ProductId),
                    Builders<Product>.Filter.Eq(p => p.TenantId, tenantId),
                    Builders<Product>.Filter.ElemMatch(
                        p => p.Variants,
                        Builders<ProductVariant>.Filter.And(
                            Builders<ProductVariant>.Filter.Eq(v => v.Id, line.VariantId),
                            Builders<ProductVariant>.Filter.Gte(v => v.StockOnHand, line.Quantity))));

                var update = Builders<Product>.Update.Inc("variants.$.stockOnHand", -line.Quantity);

                var result = await _products
                    .UpdateOneAsync(session, filter, update, options: null, cancellationToken)
                    .ConfigureAwait(false);

                if (result.ModifiedCount == 0)
                {
                    // بررسی اینکه آیا کالا ردیابی موجودی می‌شود یا خیر
                    var productFilter = Builders<Product>.Filter.And(
                        Builders<Product>.Filter.Eq("_id", line.ProductId),
                        Builders<Product>.Filter.Eq(p => p.TenantId, tenantId));

                    var product = await _products.Find(session, productFilter)
                        .FirstOrDefaultAsync(cancellationToken)
                        .ConfigureAwait(false);

                    var variant = product?.Variants.FirstOrDefault(v => v.Id == line.VariantId);
                    var trackInventory = variant?.TrackInventory ?? true;

                    if (trackInventory)
                    {
                        shortages.Add(new InventoryShortage(
                            line.ProductId,
                            line.VariantId,
                            line.Quantity,
                            variant?.StockOnHand ?? 0));
                    }
                }
            }

            if (shortages.Count > 0)
            {
                await session.AbortTransactionAsync(cancellationToken).ConfigureAwait(false);
                return CheckoutResult.InventoryFailure(shortages);
            }

            // درج سفارش در همان تراکنش
            await _orders.InsertOneAsync(session, order, options: null, cancellationToken).ConfigureAwait(false);

            await session.CommitTransactionAsync(cancellationToken).ConfigureAwait(false);
            return CheckoutResult.Success();
        }
        catch
        {
            try
            {
                await session.AbortTransactionAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // نادیده گرفتن خطا هنگام سقط تراکنش فرعی
            }
            throw;
        }
    }
}
