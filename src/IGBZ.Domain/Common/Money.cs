namespace IGBZ.Domain.Common;

/// <summary>
/// مبلغ پولی. همیشه <see cref="decimal"/> (هرگز double) و در MongoDB به‌صورت Decimal128 ذخیره می‌شود.
/// ارز پیش‌فرض پلتفرم ریال ایران (IRR) است؛ فیلد ارز برای فاز پرداخت بین‌المللی (بخش ۷.۴) رزرو شده.
/// </summary>
public readonly record struct Money : IComparable<Money>
{
    public const string DefaultCurrency = "IRR";

    private readonly string? _currency;

    public decimal Amount { get; }

    /// <summary>در حالت <c>default(Money)</c> ارز پیش‌فرض برگردانده می‌شود تا هرگز null نشود.</summary>
    public string Currency => _currency ?? DefaultCurrency;

    public Money(decimal amount, string currency = DefaultCurrency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency is required.", nameof(currency));
        }

        Amount = amount;
        _currency = currency.ToUpperInvariant();
    }

    public static Money Zero(string currency = DefaultCurrency) => new(0m, currency);

    public bool IsZero => Amount == 0m;

    public bool IsNegative => Amount < 0m;

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount - right.Amount, left.Currency);
    }

    public static Money operator *(Money value, int multiplier) =>
        new(value.Amount * multiplier, value.Currency);

    public static Money operator *(Money value, decimal multiplier) =>
        new(value.Amount * multiplier, value.Currency);

    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;

    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;

    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;

    /// <summary>سقف‌گذاری: مبلغ هرگز منفی نشود (مثلاً تخفیف بزرگ‌تر از جمع سبد).</summary>
    public Money ClampToZero() => IsNegative ? Zero(Currency) : this;

    /// <summary>کوچک‌ترین مقدار بین دو مبلغ.</summary>
    public static Money Min(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount <= right.Amount ? left : right;
    }

    /// <summary>
    /// گرد کردن طبق سیاست پلتفرم. برای ریال، واحد گرد کردن پیش‌فرض ۱ (بدون اعشار) است.
    /// </summary>
    public Money Round(RoundingPolicy? policy = null) =>
        new((policy ?? RoundingPolicy.ForCurrency(Currency)).Apply(Amount), Currency);

    public int CompareTo(Money other)
    {
        EnsureSameCurrency(this, other);
        return Amount.CompareTo(other.Amount);
    }

    public bool Equals(Money other) =>
        Amount == other.Amount
        && string.Equals(Currency, other.Currency, StringComparison.Ordinal);

    public override int GetHashCode() => HashCode.Combine(Amount, Currency);

    public override string ToString() => $"{Amount} {Currency}";

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (!string.Equals(left.Currency, right.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Currency mismatch: '{left.Currency}' vs '{right.Currency}'.");
        }
    }
}
