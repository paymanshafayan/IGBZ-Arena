using System.Collections.Concurrent;
using FluentAssertions;
using IGBZ.Application.Abstractions;
using IGBZ.Domain.Catalog;
using IGBZ.Domain.Common;
using IGBZ.Domain.Ordering;
using IGBZ.Domain.Tenancy;
using IGBZ.Infrastructure.Mongo;
using MongoDB.Driver;
using Xunit;

namespace IGBZ.Application.Tests;

/// <summary>
/// آزمون‌های یکپارچگی تسویه‌حساب و رزرو اتمی تحت بار هم‌زمان روی مونو‌دی‌بی واقعی (گام ۳).
/// </summary>
public class CheckoutIntegrationTests
{
    private const string ConnectionString = "mongodb://localhost:27017/?replicaSet=rs0&connectTimeout=2s";

    private static bool IsMongoAvailable()
    {
        try
        {
            var client = new MongoClient(ConnectionString);
            client.ListDatabaseNames();
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task Concurrent_checkouts_must_be_atomic_and_never_overallocate_stock()
    {
        // در صورتی که مونو‌دی‌بی در دسترس نباشد (مثلاً محیط توسعه محلی بدون داکر)، آزمون نادیده گرفته می‌شود.
        if (!IsMongoAvailable())
        {
            return;
        }

        // یک دیتابیس منحصربه‌فرد برای این اجرای آزمون می‌سازیم تا تداخلی ایجاد نشود.
        var dbName = $"igbz_integration_test_{Guid.NewGuid():N}";
        var client = new MongoClient(ConnectionString);
        var db = client.GetDatabase(dbName);

        try
        {
            // مقداردهی ایندکس‌ها
            await MongoInventoryService.EnsureIndexesAsync(db);

            var tenantIdString = $"tenant_{Guid.NewGuid():N}";
            var tenantId = new TenantId(tenantIdString);

            var tenantContext = new TenantContext();
            tenantContext.Set(tenantId);

            var checkoutService = new MongoCheckoutService(db, tenantContext);

            // ایجاد محصول با موجودی اولیه ۱۰ عدد از یک گونه
            var productId = $"prod_{Guid.NewGuid():N}";
            var variantId = "var-1";
            var initialStock = 10;
            var product = new Product(
                productId,
                tenantId,
                "تی‌شرت نخی",
                "tshirt",
                ProductKind.Physical,
                [new ProductVariant(variantId, "TSH-RED-L", new Money(250_000m), initialStock)]);

            var productsCollection = db.GetCollection<Product>(MongoCollections.Products);
            await productsCollection.InsertOneAsync(product);

            // تلاش برای ثبت ۱۵ سفارش هم‌زمان، هر کدام حاوی ۱ عدد از کالا
            var totalAttempts = 15;
            var tasks = new List<Task<CheckoutResult>>();

            for (int i = 0; i < totalAttempts; i++)
            {
                var orderId = $"ord_{Guid.NewGuid():N}";
                var orderNumber = $"O-{i:D3}";
                var order = new Order(
                    orderId,
                    tenantId,
                    orderNumber,
                    "cust-123",
                    [new OrderLine(productId, variantId, "تی‌شرت", "قرمز", new Money(250_000m), 1)]);

                tasks.Add(Task.Run(() => checkoutService.CheckoutAsync(order)));
            }

            var results = await Task.WhenAll(tasks);

            // بررسی نتایج
            var successes = results.Where(r => r.Succeeded).ToList();
            var failures = results.Where(r => !r.Succeeded).ToList();

            // دقیقاً باید ۱۰ سفارش با موفقیت ثبت شده باشند و ۵ سفارش با کمبود موجودی مواجه شده باشند.
            successes.Should().HaveCount(initialStock);
            failures.Should().HaveCount(totalAttempts - initialStock);

            foreach (var fail in failures)
            {
                fail.Shortages.Should().ContainSingle();
                fail.Shortages[0].ProductId.Should().Be(productId);
                fail.Shortages[0].VariantId.Should().Be(variantId);
            }

            // بررسی موجودی نهایی در دیتابیس که باید دقیقاً صفر باشد
            var updatedProduct = await productsCollection
                .Find(p => p.Id == productId)
                .SingleOrDefaultAsync();

            updatedProduct.Should().NotBeNull();
            updatedProduct.Variants[0].StockOnHand.Should().Be(0);

            // بررسی تعداد سفارش‌های ثبت شده در پایگاه داده
            var ordersCollection = db.GetCollection<Order>(MongoCollections.Orders);
            var registeredOrdersCount = await ordersCollection.CountDocumentsAsync(FilterDefinition<Order>.Empty);
            registeredOrdersCount.Should().Be(initialStock);
        }
        finally
        {
            // پاکسازی دیتابیس تستی پس از اتمام آزمون
            await client.DropDatabaseAsync(dbName);
        }
    }
}
