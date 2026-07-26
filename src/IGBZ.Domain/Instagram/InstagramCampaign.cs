using IGBZ.Domain.Tenancy;

namespace IGBZ.Domain.Instagram;

public enum CampaignKind
{
    CommentToDirect = 0,
    MentionStory = 1,
}

/// <summary>
/// کمپین اتوماسیون اینستاگرام (فاز ۸).
/// </summary>
public sealed class InstagramCampaign : TenantEntity
{
    public InstagramCampaign(
        string id,
        TenantId tenantId,
        string title,
        CampaignKind kind,
        string keyword,
        string responseTemplate,
        string? couponCodeToAttach = null)
        : base(id, tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyword);
        ArgumentException.ThrowIfNullOrWhiteSpace(responseTemplate);

        Title = title;
        Kind = kind;
        Keyword = keyword.Trim().ToLowerInvariant();
        ResponseTemplate = responseTemplate;
        CouponCodeToAttach = couponCodeToAttach;
    }

    /// <summary>سازنده بازسازی برای لایه ذخیره‌سازی.</summary>
    public InstagramCampaign(
        string id,
        string tenantId,
        string title,
        CampaignKind kind,
        string keyword,
        string responseTemplate,
        string? couponCodeToAttach,
        bool isActive,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? updatedAtUtc)
        : base(id, new TenantId(tenantId))
    {
        Title = title;
        Kind = kind;
        Keyword = keyword;
        ResponseTemplate = responseTemplate;
        CouponCodeToAttach = couponCodeToAttach;
        IsActive = isActive;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public string Title { get; private set; }

    public CampaignKind Kind { get; private set; }

    public string Keyword { get; private set; }

    public string ResponseTemplate { get; private set; }

    public string? CouponCodeToAttach { get; private set; }

    public bool IsActive { get; private set; } = true;

    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }
}
