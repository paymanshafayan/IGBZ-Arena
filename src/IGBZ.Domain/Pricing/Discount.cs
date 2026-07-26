using IGBZ.Domain.Common;
using IGBZ.Domain.Ordering;
using IGBZ.Domain.Tenancy;

namespace IGBZ.Domain.Pricing;

public enum DiscountType
{
    Percentage = 0,
    FixedAmount = 1,
}

/// <summary>
/// تخفیف — سطح ۲ طبق تصمیم تایید‌شده: درصدی/مبلغ ثابت + کد کوپن، بدون ترکیب هم‌زمان.
/// ساختار Rule-based است تا ارتقا به سطح ۳ (ترکیب/اولویت) نیاز به بازنویسی نداشته باشد:
/// کافی است <c>IDiscountSelectionStrategy</c> عوض شود.
/// </summary>
public sealed class Discount : TenantEntity
{
    public Discount(
        string id,
        TenantId tenantId,
        string name,
        DiscountType type,
        decimal value,
        string? couponCode = null,
        Money? minimumOrderSubTotal = null,
        Money? maximumDiscountAmount = null,
        DateTimeOffset? startsAtUtc = null,
        DateTimeOffset? endsAtUtc = null,
        int? usageLimit = null,
        int priority = 0)
        : base(id, tenantId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Discount name is required.", nameof(name));
        }

        if (value <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Discount value must be positive.");
        }

        if (type == DiscountType.Percentage && value > 100m)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Percentage discount cannot exceed 100.");
        }

        if (startsAtUtc.HasValue && endsAtUtc.HasValue && endsAtUtc <= startsAtUtc)
        {
            throw new ArgumentException("End date must be after start date.", nameof(endsAtUtc));
        }

        Name = name;
        Type = type;
        Value = value;
        CouponCode = string.IsNullOrWhiteSpace(couponCode) ? null : couponCode.Trim().ToUpperInvariant();
        MinimumOrderSubTotal = minimumOrderSubTotal;
        MaximumDiscountAmount = maximumDiscountAmount;
        StartsAtUtc = startsAtUtc;
        EndsAtUtc = endsAtUtc;
        UsageLimit = usageLimit;
        Priority = priority;
    }

    public string Name { get; private set; }

    public DiscountType Type { get; private set; }

    public decimal Value { get; private set; }

    /// <summary>اگر null باشد تخفیف خودکار است؛ در غیر این صورت نیازمند کد کوپن.</summary>
    public string? CouponCode { get; private set; }

    public Money? MinimumOrderSubTotal { get; private set; }

    /// <summary>سقف مبلغ تخفیف (کاربرد رایج: «۲۰٪ تا سقف ۵۰ هزار تومان»).</summary>
    public Money? MaximumDiscountAmount { get; private set; }

    public DateTimeOffset? StartsAtUtc { get; private set; }

    public DateTimeOffset? EndsAtUtc { get; private set; }

    public int? UsageLimit { get; private set; }

    public int UsageCount { get; private set; }

    /// <summary>عدد بزرگ‌تر یعنی اولویت بالاتر — مبنای انتخاب در سطح ۲ و ترکیب در سطح ۳.</summary>
    public int Priority { get; private set; }

    public bool IsActive { get; private set; } = true;

    public DiscountSource Source => CouponCode is null ? DiscountSource.Automatic : DiscountSource.Coupon;

    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }

    public void RecordUsage()
    {
        UsageCount++;
        Touch();
    }

    /// <summary>آیا این تخفیف در شرایط داده‌شده واجد شرایط است.</summary>
    public bool IsEligible(DiscountContext context)
    {
        if (!IsActive)
        {
            return false;
        }

        if (StartsAtUtc.HasValue && context.NowUtc < StartsAtUtc.Value)
        {
            return false;
        }

        if (EndsAtUtc.HasValue && context.NowUtc >= EndsAtUtc.Value)
        {
            return false;
        }

        if (UsageLimit.HasValue && UsageCount >= UsageLimit.Value)
        {
            return false;
        }

        if (MinimumOrderSubTotal.HasValue && context.SubTotal < MinimumOrderSubTotal.Value)
        {
            return false;
        }

        if (CouponCode is not null)
        {
            var provided = context.CouponCode?.Trim().ToUpperInvariant();
            if (!string.Equals(provided, CouponCode, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>مبلغ تخفیف روی جمع اقلام؛ هرگز بیشتر از خودِ جمع اقلام نمی‌شود.</summary>
    public Money ComputeAmount(Money subTotal)
    {
        var raw = Type switch
        {
            DiscountType.Percentage => subTotal * (Value / 100m),
            DiscountType.FixedAmount => new Money(Value, subTotal.Currency),
            _ => Money.Zero(subTotal.Currency),
        };

        if (MaximumDiscountAmount.HasValue)
        {
            raw = Money.Min(raw, MaximumDiscountAmount.Value);
        }

        return Money.Min(raw, subTotal).Round();
    }
}

/// <summary>ورودی ارزیابی واجد شرایط بودن تخفیف.</summary>
public sealed record DiscountContext(
    Money SubTotal,
    DateTimeOffset NowUtc,
    string? CouponCode = null,
    string? CustomerId = null);
