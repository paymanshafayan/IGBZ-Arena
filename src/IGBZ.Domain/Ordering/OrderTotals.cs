using IGBZ.Domain.Common;

namespace IGBZ.Domain.Ordering;

/// <summary>
/// خروجی پایپ‌لاین محاسبه (بخش ۵.۲):
/// SubTotal → Discounts → Tax → Shipping → GrandTotal
/// </summary>
public sealed record OrderTotals(
    Money SubTotal,
    Money DiscountTotal,
    Money TaxTotal,
    Money ShippingTotal,
    Money GrandTotal,
    IReadOnlyList<AppliedDiscount> AppliedDiscounts)
{
    public static OrderTotals Empty(string currency = Money.DefaultCurrency) =>
        new(
            Money.Zero(currency),
            Money.Zero(currency),
            Money.Zero(currency),
            Money.Zero(currency),
            Money.Zero(currency),
            []);

    /// <summary>مبلغ مشمول مالیات = جمع اقلام منهای تخفیف.</summary>
    public Money TaxableBase => (SubTotal - DiscountTotal).ClampToZero();
}

/// <summary>سند ردیابی یک تخفیف اعمال‌شده — برای شفافیت فاکتور و دیباگ.</summary>
public sealed record AppliedDiscount(
    string DiscountId,
    string Name,
    DiscountSource Source,
    Money Amount,
    string? CouponCode = null);

public enum DiscountSource
{
    /// <summary>کمپین خودکار فروشگاه (بدون کد).</summary>
    Automatic = 0,

    /// <summary>کد کوپن واردشده توسط مشتری.</summary>
    Coupon = 1,
}
