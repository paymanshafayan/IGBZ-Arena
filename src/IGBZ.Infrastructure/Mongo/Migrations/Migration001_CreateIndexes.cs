using IGBZ.Domain.Catalog;
using IGBZ.Domain.Ordering;
using IGBZ.Domain.Pricing;
using MongoDB.Driver;

namespace IGBZ.Infrastructure.Mongo.Migrations;

/// <summary>
/// فاز اول - ساخت خودکار ایندکس‌های تکمیلی و ترکیبی تننت‌دار در شروع پلتفرم.
/// </summary>
public sealed class Migration001_CreateIndexes : IMongoMigration
{
    public int Version => 1;

    public string Description => "Create core composite indexes for multi-tenant isolation and search optimization.";

    public async Task UpAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        // ۱. ایندکس‌های کاتالوگ محصولات
        var products = database.GetCollection<Product>(MongoCollections.Products);
        await products.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<Product>(
                    Builders<Product>.IndexKeys
                        .Ascending(p => p.TenantId)
                        .Ascending(p => p.Slug),
                    new CreateIndexOptions { Unique = true }),
                new CreateIndexModel<Product>(
                    Builders<Product>.IndexKeys
                        .Ascending(p => p.TenantId)
                        .Ascending("variants.id"))
            ],
            cancellationToken).ConfigureAwait(false);

        // ۲. ایندکس‌های سفارشات
        var orders = database.GetCollection<Order>(MongoCollections.Orders);
        await orders.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<Order>(
                    Builders<Order>.IndexKeys
                        .Ascending(o => o.TenantId)
                        .Ascending(o => o.OrderNumber),
                    new CreateIndexOptions { Unique = true }),
                new CreateIndexModel<Order>(
                    Builders<Order>.IndexKeys
                        .Ascending(o => o.TenantId)
                        .Ascending(o => o.CustomerId))
            ],
            cancellationToken).ConfigureAwait(false);

        // ۳. ایندکس‌های تخفیف‌ها
        var discounts = database.GetCollection<Discount>(MongoCollections.Discounts);
        await discounts.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<Discount>(
                    Builders<Discount>.IndexKeys
                        .Ascending(d => d.TenantId)
                        .Ascending(d => d.CouponCode))
            ],
            cancellationToken).ConfigureAwait(false);
    }
}
