namespace IGBZ.Domain.Common;

/// <summary>
/// سیاست گرد کردن مبالغ. مطابق بازبینی معماری، این سیاست باید صریح باشد تا محاسبهٔ
/// مالیات/تخفیف قابل بازتولید و قابل تطبیق با فاکتور مؤدیان باشد.
/// </summary>
public sealed record RoundingPolicy(int Decimals, decimal Step, MidpointRounding Mode)
{
    /// <summary>ریال: بدون اعشار، گام ۱ ریال.</summary>
    public static readonly RoundingPolicy Rial = new(0, 1m, MidpointRounding.AwayFromZero);

    /// <summary>ارزهای دو رقم اعشاری (برای فاز بین‌المللی).</summary>
    public static readonly RoundingPolicy TwoDecimals = new(2, 0.01m, MidpointRounding.AwayFromZero);

    public static RoundingPolicy ForCurrency(string currency) =>
        string.Equals(currency, Money.DefaultCurrency, StringComparison.OrdinalIgnoreCase)
            ? Rial
            : TwoDecimals;

    public decimal Apply(decimal value)
    {
        if (Step <= 0m)
        {
            throw new InvalidOperationException("Rounding step must be positive.");
        }

        var steps = Math.Round(value / Step, 0, Mode);
        return Math.Round(steps * Step, Decimals, Mode);
    }
}
