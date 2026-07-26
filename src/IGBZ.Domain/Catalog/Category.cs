using IGBZ.Domain.Tenancy;

namespace IGBZ.Domain.Catalog;

/// <summary>
/// دسته‌بندی محصولات فروشگاه (فاز ۳ - بخش ۱ نقشه‌ی راه).
/// </summary>
public sealed class Category : TenantEntity
{
    public Category(
        string id,
        TenantId tenantId,
        string name,
        string slug,
        int displayOrder = 0,
        string? parentCategoryId = null)
        : base(id, tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        Name = name;
        Slug = slug.Trim().ToLowerInvariant();
        DisplayOrder = displayOrder;
        ParentCategoryId = parentCategoryId;
    }

    /// <summary>سازنده بازسازی برای لایه ذخیره‌سازی.</summary>
    public Category(
        string id,
        string tenantId,
        string name,
        string slug,
        int displayOrder,
        string? parentCategoryId,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? updatedAtUtc)
        : base(id, new TenantId(tenantId))
    {
        Name = name;
        Slug = slug;
        DisplayOrder = displayOrder;
        ParentCategoryId = parentCategoryId;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public string Name { get; private set; }

    public string Slug { get; private set; }

    public int DisplayOrder { get; private set; }

    public string? ParentCategoryId { get; private set; }
}
