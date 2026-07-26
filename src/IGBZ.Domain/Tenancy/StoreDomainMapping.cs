using IGBZ.Domain.Tenancy;

namespace IGBZ.Domain.Tenancy;

/// <summary>
/// نگاشت دامنه‌های اختصاصی مشتریان به مستأجر (بخش ۶ - فاز ۲).
/// </summary>
public sealed class StoreDomainMapping : TenantEntity
{
    public StoreDomainMapping(
        string id,
        TenantId tenantId,
        string customDomain,
        string verificationToken)
        : base(id, tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customDomain);
        ArgumentException.ThrowIfNullOrWhiteSpace(verificationToken);

        CustomDomain = customDomain.Trim().ToLowerInvariant();
        VerificationToken = verificationToken;
    }

    /// <summary>سازنده بازسازی برای لایه ذخیره‌سازی.</summary>
    public StoreDomainMapping(
        string id,
        string tenantId,
        string customDomain,
        string verificationToken,
        bool isVerified,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? updatedAtUtc)
        : base(id, new TenantId(tenantId))
    {
        CustomDomain = customDomain;
        VerificationToken = verificationToken;
        IsVerified = isVerified;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public string CustomDomain { get; private set; }

    public string VerificationToken { get; private set; }

    public bool IsVerified { get; private set; }

    public void Verify()
    {
        IsVerified = true;
        Touch();
    }
}
