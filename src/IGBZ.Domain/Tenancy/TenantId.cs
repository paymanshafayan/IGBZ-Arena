namespace IGBZ.Domain.Tenancy;

/// <summary>
/// شناسهٔ مستأجر. یک Value Object جدا (نه string خام) تا امکان جاگذاری اشتباهی
/// یک شناسهٔ دلخواه در کوئری‌ها کم شود و جداسازی چندمستأجری در سطح تایپ دیده شود.
/// </summary>
public readonly record struct TenantId
{
    /// <summary>سایت مادر (Platform) — شبه‌تننت طبق بخش ۴ سند معماری.</summary>
    public const string PlatformValue = "platform";

    public static readonly TenantId Platform = new(PlatformValue);

    public string Value { get; }

    public TenantId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("TenantId cannot be empty.", nameof(value));
        }

        var normalized = value.Trim().ToLowerInvariant();

        if (normalized.Length > 64)
        {
            throw new ArgumentException("TenantId is too long.", nameof(value));
        }

        foreach (var ch in normalized)
        {
            var isAllowed = ch is >= 'a' and <= 'z' || ch is >= '0' and <= '9' || ch is '-';
            if (!isAllowed)
            {
                throw new ArgumentException(
                    $"TenantId contains an invalid character: '{ch}'.", nameof(value));
            }
        }

        Value = normalized;
    }

    public bool IsPlatform => Value == PlatformValue;

    public override string ToString() => Value;

    public static implicit operator string(TenantId id) => id.Value;
}
