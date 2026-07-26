using IGBZ.Application.Abstractions;
using IGBZ.Domain.Catalog;
using MongoDB.Bson;
using MongoDB.Driver;

namespace IGBZ.Infrastructure.Mongo;

/// <summary>
/// رزرو موجودی با عملیات Atomic روی MongoDB (بخش ۵.۴).
///
/// هر قلم با یک <c>UpdateOne</c> شرطی کم می‌شود:
///   filter: { _id, tenantId, variants: { $elemMatch: { id, stockOnHand: { $gte: qty } } } }
///   update: { $inc: { "variants.$.stockOnHand": -qty } }
/// اگر <c>ModifiedCount == 0</c> یعنی موجودی کافی نبود.
///
/// برای اتمی‌بودن کل سبد (همه یا هیچ) از تراکنش استفاده می‌شود؛ این نیازمند اجرای
/// MongoDB به‌صورت Replica Set است (حتی تک‌نودی). اگر Session پشتیبانی نشود،
/// به مسیر جبرانی (Compensating release) برمی‌گردیم.
/// </summary>
public sealed class MongoInventoryService(
    IMongoDatabase database,
    ITenantContext tenantContext,
    string collectionName = MongoCollections.Products) : IInventoryService
{
    private readonly IMongoCollection<Product> _products = database.GetCollection<Product>(collectionName);
    private readonly IMongoClient _client = database.Client;

    public async Task<InventoryReservationResult> TryReserveAsync(
        IReadOnlyList<InventoryReservationRequest> items,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0)
        {
            return InventoryReservationResult.Success([]);
        }

        using var session = await _client.StartSessionAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var reserved = new List<InventoryReservationRequest>(items.Count);
        var shortages = new List<InventoryShortage>();

        session.StartTransaction();
        try
        {
            foreach (var item in items)
            {
                var modified = await DecrementAsync(session, item, cancellationToken).ConfigureAwait(false);
                if (modified)
                {
                    reserved.Add(item);
                }
                else
                {
                    shortages.Add(await DescribeShortageAsync(item, cancellationToken).ConfigureAwait(false));
                }
            }

            if (shortages.Count > 0)
            {
                await session.AbortTransactionAsync(cancellationToken).ConfigureAwait(false);
                return InventoryReservationResult.Failure(shortages);
            }

            await session.CommitTransactionAsync(cancellationToken).ConfigureAwait(false);
            return InventoryReservationResult.Success(reserved);
        }
        catch
        {
            await session.AbortTransactionAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public async Task ReleaseAsync(
        IReadOnlyList<InventoryReservationRequest> items,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        foreach (var item in items)
        {
            var filter = Builders<Product>.Filter.And(
                Builders<Product>.Filter.Eq("_id", item.ProductId),
                Builders<Product>.Filter.Eq(p => p.TenantId, tenantContext.Current.Value),
                Builders<Product>.Filter.ElemMatch(
                    p => p.Variants,
                    Builders<ProductVariant>.Filter.Eq(v => v.Id, item.VariantId)));

            var update = Builders<Product>.Update.Inc("variants.$.stockOnHand", item.Quantity);

            await _products.UpdateOneAsync(filter, update, options: null, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task<bool> DecrementAsync(
        IClientSessionHandle session,
        InventoryReservationRequest item,
        CancellationToken cancellationToken)
    {
        var filter = Builders<Product>.Filter.And(
            Builders<Product>.Filter.Eq("_id", item.ProductId),
            Builders<Product>.Filter.Eq(p => p.TenantId, tenantContext.Current.Value),
            Builders<Product>.Filter.ElemMatch(
                p => p.Variants,
                Builders<ProductVariant>.Filter.And(
                    Builders<ProductVariant>.Filter.Eq(v => v.Id, item.VariantId),
                    Builders<ProductVariant>.Filter.Gte(v => v.StockOnHand, item.Quantity))));

        var update = Builders<Product>.Update.Inc("variants.$.stockOnHand", -item.Quantity);

        var result = await _products
            .UpdateOneAsync(session, filter, update, options: null, cancellationToken)
            .ConfigureAwait(false);

        if (result.ModifiedCount > 0)
        {
            return true;
        }

        // گونه‌هایی که موجودی‌شان ردیابی نمی‌شود همیشه در دسترس‌اند.
        return await IsUntrackedAsync(item, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> IsUntrackedAsync(
        InventoryReservationRequest item,
        CancellationToken cancellationToken)
    {
        var variant = await LoadVariantAsync(item, cancellationToken).ConfigureAwait(false);
        return variant is { TrackInventory: false };
    }

    private async Task<InventoryShortage> DescribeShortageAsync(
        InventoryReservationRequest item,
        CancellationToken cancellationToken)
    {
        var variant = await LoadVariantAsync(item, cancellationToken).ConfigureAwait(false);
        return new InventoryShortage(item.ProductId, item.VariantId, item.Quantity, variant?.StockOnHand ?? 0);
    }

    private async Task<ProductVariant?> LoadVariantAsync(
        InventoryReservationRequest item,
        CancellationToken cancellationToken)
    {
        var filter = Builders<Product>.Filter.And(
            Builders<Product>.Filter.Eq("_id", item.ProductId),
            Builders<Product>.Filter.Eq(p => p.TenantId, tenantContext.Current.Value));

        var product = await _products.Find(filter)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return product?.Variants.FirstOrDefault(v => v.Id == item.VariantId);
    }

    /// <summary>ایندکس‌های لازم برای عملکرد رزرو.</summary>
    public static async Task EnsureIndexesAsync(IMongoDatabase db, CancellationToken cancellationToken = default)
    {
        var products = db.GetCollection<Product>(MongoCollections.Products);
        await products.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<Product>(
                    Builders<Product>.IndexKeys
                        .Ascending(p => p.TenantId)
                        .Ascending("variants.id")),
                new CreateIndexModel<Product>(
                    Builders<Product>.IndexKeys
                        .Ascending(p => p.TenantId)
                        .Ascending(p => p.Slug),
                    new CreateIndexOptions { Unique = true }),
            ],
            cancellationToken).ConfigureAwait(false);
    }

    internal static BsonDocument DebugFilterShape(string tenantId, string productId, string variantId, int qty) =>
        new()
        {
            ["_id"] = productId,
            ["tenantId"] = tenantId,
            ["variants"] = new BsonDocument("$elemMatch", new BsonDocument
            {
                ["id"] = variantId,
                ["stockOnHand"] = new BsonDocument("$gte", qty),
            }),
        };
}
