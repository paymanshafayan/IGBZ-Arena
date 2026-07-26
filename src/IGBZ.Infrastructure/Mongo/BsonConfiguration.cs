using IGBZ.Domain.Common;
using MongoDB.Bson;
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
