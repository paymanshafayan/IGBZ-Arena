using IGBZ.Domain.Common;
using IGBZ.Domain.Ordering;
using IGBZ.Domain.Pricing;

namespace IGBZ.Application.Pricing;

/// <summary>ورودی پایپ‌لاین محاسبه (بخش ۵.۲).</summary>
public sealed record PricingRequest(
    IReadOnlyList<OrderLine> Lines,
    IReadOnlyList<Discount> AvailableDiscounts,
    string? CouponCode,
    ShippingQuote Shipping,
    TaxSettings Tax,
    DateTimeOffset NowUtc,
    string Currency = Money.DefaultCurrency,
    string? CustomerId = null);

public sealed record ShippingQuote(Money Cost, string MethodCode, bool IsTaxable = false)
{
    public static ShippingQuote Free(string currency = Money.DefaultCurrency) =>
        new(Money.Zero(currency), "free");
}

/// <summary>بخش ۵.۳: نرخ ثابت VAT.</summary>
public sealed record TaxSettings(decimal VatRatePercent, bool PricesIncludeTax = false)
{
    /// <summary>نرخ رایج مالیات بر ارزش افزوده در ایران؛ قابل تنظیم per-tenant.</summary>
    public static readonly TaxSettings DefaultIran = new(10m);

    public static readonly TaxSettings None = new(0m);
}

public interface ISubTotalCalculator
{
    Money Calculate(PricingRequest request);
}

public interface IDiscountCalculator
{
    IReadOnlyList<AppliedDiscount> Calculate(PricingRequest request, Money subTotal);
}

public interface ITaxCalculator
{
    Money Calculate(PricingRequest request, Money taxableBase);
}

public interface IShippingCalculator
{
    Money Calculate(PricingRequest request);
}

public sealed class SubTotalCalculator : ISubTotalCalculator
{
    public Money Calculate(PricingRequest request)
    {
        var total = Money.Zero(request.Currency);
        foreach (var line in request.Lines)
        {
            total += line.LineTotal;
        }

        return total.Round();
    }
}

/// <summary>
/// سطح ۲: بهترین تخفیف واجد شرایط انتخاب می‌شود (بدون ترکیب هم‌زمان).
/// معیار انتخاب: ابتدا Priority بالاتر، سپس مبلغ تخفیف بیشتر به نفع مشتری.
/// برای ارتقا به سطح ۳ فقط همین کلاس جایگزین می‌شود.
/// </summary>
public sealed class BestSingleDiscountCalculator : IDiscountCalculator
{
    public IReadOnlyList<AppliedDiscount> Calculate(PricingRequest request, Money subTotal)
    {
        if (subTotal.IsZero || request.AvailableDiscounts.Count == 0)
        {
            return [];
        }

        var context = new DiscountContext(subTotal, request.NowUtc, request.CouponCode, request.CustomerId);

        var best = request.AvailableDiscounts
            .Where(d => d.IsEligible(context))
            .Select(d => new { Discount = d, Amount = d.ComputeAmount(subTotal) })
            .Where(x => !x.Amount.IsZero)
            .OrderByDescending(x => x.Discount.Priority)
            .ThenByDescending(x => x.Amount.Amount)
            .FirstOrDefault();

        if (best is null)
        {
            return [];
        }

        return
        [
            new AppliedDiscount(
                best.Discount.Id,
                best.Discount.Name,
                best.Discount.Source,
                best.Amount,
                best.Discount.CouponCode),
        ];
    }
}

public sealed class FlatVatCalculator : ITaxCalculator
{
    public Money Calculate(PricingRequest request, Money taxableBase)
    {
        var rate = request.Tax.VatRatePercent;
        if (rate <= 0m || taxableBase.IsZero)
        {
            return Money.Zero(request.Currency);
        }

        if (request.Tax.PricesIncludeTax)
        {
            // قیمت‌ها شامل مالیات‌اند: مالیات مستتر استخراج می‌شود.
            return (taxableBase * (rate / (100m + rate))).Round();
        }

        return (taxableBase * (rate / 100m)).Round();
    }
}

public sealed class QuoteShippingCalculator : IShippingCalculator
{
    public Money Calculate(PricingRequest request) => request.Shipping.Cost.Round();
}
