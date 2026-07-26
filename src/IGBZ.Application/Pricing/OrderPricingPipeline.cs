using IGBZ.Domain.Common;
using IGBZ.Domain.Ordering;

namespace IGBZ.Application.Pricing;

public interface IOrderPricingPipeline
{
    OrderTotals Calculate(PricingRequest request);
}

/// <summary>
/// پایپ‌لاین محاسبهٔ بخش ۵.۲:
/// SubTotal → Discounts → Tax (VAT ثابت) → Shipping → GrandTotal
/// هر مرحله یک Calculator مستقل و تست‌پذیر است؛ ترتیب اینجا و فقط اینجا تعریف می‌شود.
/// </summary>
public sealed class OrderPricingPipeline(
    ISubTotalCalculator subTotalCalculator,
    IDiscountCalculator discountCalculator,
    ITaxCalculator taxCalculator,
    IShippingCalculator shippingCalculator) : IOrderPricingPipeline
{
    public OrderTotals Calculate(PricingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Lines.Count == 0)
        {
            return OrderTotals.Empty(request.Currency);
        }

        // ۱) جمع اقلام
        var subTotal = subTotalCalculator.Calculate(request);

        // ۲) تخفیف‌ها
        var applied = discountCalculator.Calculate(request, subTotal);
        var discountTotal = Money.Zero(request.Currency);
        foreach (var item in applied)
        {
            discountTotal += item.Amount;
        }

        discountTotal = Money.Min(discountTotal, subTotal);

        // ۳) مالیات روی مبلغ پس از تخفیف
        var taxableBase = (subTotal - discountTotal).ClampToZero();
        var tax = taxCalculator.Calculate(request, taxableBase);

        // ۴) هزینهٔ ارسال (در صورت مشمول بودن، مالیاتش جدا اضافه می‌شود)
        var shipping = shippingCalculator.Calculate(request);
        if (request.Shipping.IsTaxable && !shipping.IsZero)
        {
            tax += taxCalculator.Calculate(request, shipping);
        }

        // ۵) جمع نهایی
        var grandTotal = (taxableBase + tax + shipping).ClampToZero().Round();

        return new OrderTotals(subTotal, discountTotal, tax, shipping, grandTotal, applied);
    }
}
