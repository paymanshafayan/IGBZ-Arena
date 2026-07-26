namespace IGBZ.Domain.Tenancy;

public enum TenantStatus
{
    Provisioning = 0,
    Active = 1,
    Suspended = 2,
    Cancelled = 3,
}

/// <summary>
/// موجودیت سطح پلتفرم (بدون <c>tenantId</c> — خودش تننت است).
/// در Collection <c>platform_tenants</c> زندگی می‌کند و از مسیر
/// <c>ITenantScopedRepository</c> عبور نمی‌کند.
/// </summary>
public sealed class Tenant
{
    public Tenant(string id, string storeName, string subdomain, string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);

        Id = new TenantId(id).Value;
        StoreName = storeName;
        Subdomain = NormalizeSubdomain(subdomain);
        PlanId = planId;
    }

    public string Id { get; private set; }

    public string StoreName { get; private set; }

    public string Subdomain { get; private set; }

    public string? CustomDomain { get; private set; }

    public string PlanId { get; private set; }

    public TenantStatus Status { get; private set; } = TenantStatus.Provisioning;

    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ActivatedAtUtc { get; private set; }

    /// <summary>نرخ VAT مخصوص این فروشگاه (بخش ۵.۳ — نرخ ثابت، قابل تنظیم per-tenant).</summary>
    public decimal VatRatePercent { get; private set; } = 10m;

    public void Activate()
    {
        if (Status is TenantStatus.Cancelled)
        {
            throw new InvalidOperationException("A cancelled tenant cannot be activated.");
        }

        Status = TenantStatus.Active;
        ActivatedAtUtc ??= DateTimeOffset.UtcNow;
    }

    public void Suspend() => Status = TenantStatus.Suspended;

    public void Cancel() => Status = TenantStatus.Cancelled;

    public void AttachCustomDomain(string domain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        CustomDomain = domain.Trim().ToLowerInvariant();
    }

    public void SetVatRate(decimal ratePercent)
    {
        if (ratePercent is < 0m or > 100m)
        {
            throw new ArgumentOutOfRangeException(nameof(ratePercent));
        }

        VatRatePercent = ratePercent;
    }

    private static string NormalizeSubdomain(string subdomain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subdomain);
        var value = subdomain.Trim().ToLowerInvariant();

        if (value.Length is < 3 or > 40)
        {
            throw new ArgumentException("Subdomain must be between 3 and 40 characters.", nameof(subdomain));
        }

        if (value.StartsWith('-') || value.EndsWith('-'))
        {
            throw new ArgumentException("Subdomain cannot start or end with a hyphen.", nameof(subdomain));
        }

        foreach (var ch in value)
        {
            var ok = ch is >= 'a' and <= 'z' || ch is >= '0' and <= '9' || ch is '-';
            if (!ok)
            {
                throw new ArgumentException($"Invalid character in subdomain: '{ch}'.", nameof(subdomain));
            }
        }

        if (ReservedSubdomains.Contains(value))
        {
            throw new ArgumentException($"Subdomain '{value}' is reserved.", nameof(subdomain));
        }

        return value;
    }

    public static readonly IReadOnlySet<string> ReservedSubdomains = new HashSet<string>(StringComparer.Ordinal)
    {
        "www", "api", "admin", "app", "cdn", "static", "mail", "platform", "igbz", "status", "docs", "blog",
    };
}
