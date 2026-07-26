using IGBZ.Domain.Common;
using IGBZ.Domain.Catalog;
using IGBZ.Domain.Ordering;
using IGBZ.Domain.Pricing;
using IGBZ.Domain.Tenancy;
using IGBZ.Domain.Instagram;
using IGBZ.Domain.Lms;
using IGBZ.Domain.Integration;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

namespace IGBZ.Infrastructure.Mongo;

/// <summary>
/// نگاشت BSON. باید یک بار در شروع برنامه فراخوانی شود.
///
/// نکات کلیدی:
/// - همهٔ مبالغ به‌صورت <c>Decimal128</c> ذخیره می‌شوند (نه double) — ADR-0002.
/// - نام فیلدها camelCase است تا با قرارداد <c>tenantId</c> سند معماری بخواند.
/// - Enumها به‌صورت رشته ذخیره می‌شوند تا تغییر ترتیب مقادیر داده را خراب نکند.
///
/// TODO(فاز ۱): نگاشت سازنده‌محور برای <c>Product</c>/<c>Order</c>/<c>Discount</c>.
/// این موجودیت‌ها عمداً سازندهٔ بدون پارامتر ندارند (Aggregate محافظت‌شده) و
/// <c>TenantId</c> در سازنده Value Object ولی در پراپرتی <c>string</c> است، پس
/// AutoMap کافی نیست و باید ClassMap صریح با <c>MapCreator</c> نوشته شود.
/// تا آن زمان فقط قراردادهای عمومی و سریالایزر <see cref="Money"/> ثبت می‌شوند.
/// </summary>
public static class BsonConfiguration
{
    private static bool _registered;
    private static readonly object Gate = new();

    public static void Register()
    {
        lock (Gate)
        {
            if (_registered)
            {
                return;
            }

            var pack = new ConventionPack
            {
                new CamelCaseElementNameConvention(),
                new IgnoreExtraElementsConvention(true),
                new EnumRepresentationConvention(BsonType.String),
                new ImmutableTypeClassMapConvention(),
            };

            ConventionRegistry.Register("igbz", pack, _ => true);

            BsonSerializer.RegisterSerializer(
                typeof(decimal),
                new DecimalSerializer(BsonType.Decimal128));

            BsonSerializer.RegisterSerializer(
                typeof(decimal?),
                new NullableSerializer<decimal>(new DecimalSerializer(BsonType.Decimal128)));

            BsonSerializer.RegisterSerializer(typeof(Money), new MoneySerializer());

            BsonClassMap.RegisterClassMap<Product>(cm =>
            {
                cm.MapConstructor(typeof(Product).GetConstructor([
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(ProductKind),
                    typeof(bool),
                    typeof(IEnumerable<ProductVariant>),
                    typeof(IEnumerable<string>),
                    typeof(string),
                    typeof(DateTimeOffset),
                    typeof(DateTimeOffset?)
                ])!);
                cm.MapIdProperty(p => p.Id);
                cm.MapProperty(p => p.TenantId);
                cm.MapProperty(p => p.Name);
                cm.MapProperty(p => p.Slug);
                cm.MapProperty(p => p.Kind);
                cm.MapProperty(p => p.IsPublished);
                cm.MapProperty(p => p.CategoryIds);
                cm.MapProperty(p => p.Variants);
                cm.MapProperty(p => p.TaxpayerGoodsCode);
                cm.MapProperty(p => p.CreatedAtUtc);
                cm.MapProperty(p => p.UpdatedAtUtc);
            });

            BsonClassMap.RegisterClassMap<Order>(cm =>
            {
                cm.MapConstructor(typeof(Order).GetConstructor([
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(OrderStatus),
                    typeof(OrderTotals),
                    typeof(IEnumerable<OrderLine>),
                    typeof(IEnumerable<OrderStatusChange>),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(DateTimeOffset),
                    typeof(DateTimeOffset?)
                ])!);
                cm.MapIdProperty(p => p.Id);
                cm.MapProperty(p => p.TenantId);
                cm.MapProperty(p => p.OrderNumber);
                cm.MapProperty(p => p.CustomerId);
                cm.MapProperty(p => p.Currency);
                cm.MapProperty(p => p.Status);
                cm.MapProperty(p => p.Totals);
                cm.MapProperty(p => p.Lines);
                cm.MapProperty(p => p.History);
                cm.MapProperty(p => p.PaymentTransactionId);
                cm.MapProperty(p => p.CancellationReason);
                cm.MapProperty(p => p.TrackingCode);
                cm.MapProperty(p => p.TaxpayerInvoiceId);
                cm.MapProperty(p => p.CreatedAtUtc);
                cm.MapProperty(p => p.UpdatedAtUtc);
            });

            BsonClassMap.RegisterClassMap<Discount>(cm =>
            {
                cm.MapConstructor(typeof(Discount).GetConstructor([
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(DiscountType),
                    typeof(decimal),
                    typeof(string),
                    typeof(Money),
                    typeof(Money),
                    typeof(DateTimeOffset?),
                    typeof(DateTimeOffset?),
                    typeof(int?),
                    typeof(int),
                    typeof(int),
                    typeof(bool),
                    typeof(DateTimeOffset),
                    typeof(DateTimeOffset?)
                ])!);
                cm.MapIdProperty(p => p.Id);
                cm.MapProperty(p => p.TenantId);
                cm.MapProperty(p => p.Name);
                cm.MapProperty(p => p.Type);
                cm.MapProperty(p => p.Value);
                cm.MapProperty(p => p.CouponCode);
                cm.MapProperty(p => p.MinimumOrderSubTotal);
                cm.MapProperty(p => p.MaximumDiscountAmount);
                cm.MapProperty(p => p.StartsAtUtc);
                cm.MapProperty(p => p.EndsAtUtc);
                cm.MapProperty(p => p.UsageLimit);
                cm.MapProperty(p => p.UsageCount);
                cm.MapProperty(p => p.Priority);
                cm.MapProperty(p => p.IsActive);
                cm.MapProperty(p => p.CreatedAtUtc);
                cm.MapProperty(p => p.UpdatedAtUtc);
            });

            BsonClassMap.RegisterClassMap<Tenant>(cm =>
            {
                cm.MapConstructor(typeof(Tenant).GetConstructor([
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(TenantStatus),
                    typeof(DateTimeOffset),
                    typeof(DateTimeOffset?),
                    typeof(decimal)
                ])!);
                cm.MapIdProperty(t => t.Id);
                cm.MapProperty(t => t.StoreName);
                cm.MapProperty(t => t.Subdomain);
                cm.MapProperty(t => t.CustomDomain);
                cm.MapProperty(t => t.PlanId);
                cm.MapProperty(t => t.Status);
                cm.MapProperty(t => t.CreatedAtUtc);
                cm.MapProperty(t => t.ActivatedAtUtc);
                cm.MapProperty(t => t.VatRatePercent);
            });

            BsonClassMap.RegisterClassMap<InstagramCampaign>(cm =>
            {
                cm.MapConstructor(typeof(InstagramCampaign).GetConstructor([
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(CampaignKind),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(bool),
                    typeof(DateTimeOffset),
                    typeof(DateTimeOffset?)
                ])!);
                cm.MapIdProperty(c => c.Id);
                cm.MapProperty(c => c.TenantId);
                cm.MapProperty(c => c.Title);
                cm.MapProperty(c => c.Kind);
                cm.MapProperty(c => c.Keyword);
                cm.MapProperty(c => c.ResponseTemplate);
                cm.MapProperty(c => c.CouponCodeToAttach);
                cm.MapProperty(c => c.IsActive);
                cm.MapProperty(c => c.CreatedAtUtc);
                cm.MapProperty(c => c.UpdatedAtUtc);
            });

            BsonClassMap.RegisterClassMap<Course>(cm =>
            {
                cm.MapConstructor(typeof(Course).GetConstructor([
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(IEnumerable<CourseLesson>),
                    typeof(DateTimeOffset),
                    typeof(DateTimeOffset?)
                ])!);
                cm.MapIdProperty(c => c.Id);
                cm.MapProperty(c => c.TenantId);
                cm.MapProperty(c => c.ProductId);
                cm.MapProperty(c => c.Title);
                cm.MapProperty(c => c.Lessons);
                cm.MapProperty(c => c.CreatedAtUtc);
                cm.MapProperty(c => c.UpdatedAtUtc);
            });

            BsonClassMap.RegisterClassMap<CourseLesson>(cm =>
            {
                cm.MapConstructor(typeof(CourseLesson).GetConstructor([
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(int)
                ])!);
                cm.MapProperty(l => l.Id);
                cm.MapProperty(l => l.Title);
                cm.MapProperty(l => l.VideoHlsUrl);
                cm.MapProperty(l => l.DurationMinutes);
            });

            BsonClassMap.RegisterClassMap<Category>(cm =>
            {
                cm.MapConstructor(typeof(Category).GetConstructor([
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(int),
                    typeof(string),
                    typeof(DateTimeOffset),
                    typeof(DateTimeOffset?)
                ])!);
                cm.MapIdProperty(c => c.Id);
                cm.MapProperty(c => c.TenantId);
                cm.MapProperty(c => c.Name);
                cm.MapProperty(c => c.Slug);
                cm.MapProperty(c => c.DisplayOrder);
                cm.MapProperty(c => c.ParentCategoryId);
                cm.MapProperty(c => c.CreatedAtUtc);
                cm.MapProperty(c => c.UpdatedAtUtc);
            });

            BsonClassMap.RegisterClassMap<StoreDomainMapping>(cm =>
            {
                cm.MapConstructor(typeof(StoreDomainMapping).GetConstructor([
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(bool),
                    typeof(DateTimeOffset),
                    typeof(DateTimeOffset?)
                ])!);
                cm.MapIdProperty(d => d.Id);
                cm.MapProperty(d => d.TenantId);
                cm.MapProperty(d => d.CustomDomain);
                cm.MapProperty(d => d.VerificationToken);
                cm.MapProperty(d => d.IsVerified);
                cm.MapProperty(d => d.CreatedAtUtc);
                cm.MapProperty(d => d.UpdatedAtUtc);
            });

            BsonClassMap.RegisterClassMap<IntegrationConnection>(cm =>
            {
                cm.MapConstructor(typeof(IntegrationConnection).GetConstructor([
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(IntegrationType),
                    typeof(string),
                    typeof(bool),
                    typeof(IReadOnlyDictionary<string, string>),
                    typeof(DateTimeOffset),
                    typeof(DateTimeOffset?)
                ])!);
                cm.MapIdProperty(c => c.Id);
                cm.MapProperty(c => c.TenantId);
                cm.MapProperty(c => c.ProviderName);
                cm.MapProperty(c => c.Type);
                cm.MapProperty(c => c.ApiKey);
                cm.MapProperty(c => c.IsConnected);
                cm.MapProperty(c => c.Settings);
                cm.MapProperty(c => c.CreatedAtUtc);
                cm.MapProperty(c => c.UpdatedAtUtc);
            });

            _registered = true;
        }
    }
}

/// <summary>
/// سریالایزر <see cref="Money"/> به‌صورت سند <c>{ amount: Decimal128, currency: "IRR" }</c>.
/// نوشته‌شدن به‌صورت Decimal128 صریح است تا هیچ‌گاه به double تبدیل نشود.
/// </summary>
public sealed class MoneySerializer : SerializerBase<Money>
{
    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Money value)
    {
        var writer = context.Writer;
        writer.WriteStartDocument();
        writer.WriteName("amount");
        writer.WriteDecimal128(value.Amount);
        writer.WriteName("currency");
        writer.WriteString(value.Currency);
        writer.WriteEndDocument();
    }

    public override Money Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var reader = context.Reader;

        if (reader.GetCurrentBsonType() == BsonType.Null)
        {
            reader.ReadNull();
            return Money.Zero();
        }

        decimal amount = 0m;
        var currency = Money.DefaultCurrency;

        reader.ReadStartDocument();
        while (reader.ReadBsonType() != BsonType.EndOfDocument)
        {
            switch (reader.ReadName())
            {
                case "amount":
                    amount = (decimal)reader.ReadDecimal128();
                    break;
                case "currency":
                    currency = reader.ReadString();
                    break;
                default:
                    reader.SkipValue();
                    break;
            }
        }

        reader.ReadEndDocument();
        return new Money(amount, currency);
    }
}
