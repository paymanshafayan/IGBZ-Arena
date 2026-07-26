using IGBZ.Domain.Common;
using IGBZ.Domain.Tenancy;

namespace IGBZ.Domain.Catalog;

public enum ProductKind
{
    Physical = 0,

    /// <summary>محصول دیجیتال با فایل دانلودی.</summary>
    Digital = 1,

    /// <summary>دوره (فاز ۱۰/LMS) — بدون دانلود مستقیم، دسترسی به صفحهٔ دوره باز می‌شود.</summary>
    Course = 2,
}

/// <summary>
/// محصول با مدل Variant-based (بخش ۵.۴). موجودی روی Variant است، نه روی محصول.
/// </summary>
public sealed class Product : TenantEntity
{
    private readonly List<ProductVariant> _variants;

    public Product(
        string id,
        TenantId tenantId,
        string name,
        string slug,
        ProductKind kind,
        IEnumerable<ProductVariant> variants,
        IEnumerable<string>? categoryIds = null)
        : base(id, tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        _variants = [.. variants];
        if (_variants.Count == 0)
        {
            throw new ArgumentException("A product must have at least one variant.", nameof(variants));
        }

        var duplicateSku = _variants
            .GroupBy(v => v.Sku, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicateSku is not null)
        {
            throw new ArgumentException($"Duplicate SKU '{duplicateSku.Key}' in product variants.", nameof(variants));
        }

        Name = name;
        Slug = slug.Trim().ToLowerInvariant();
        Kind = kind;
        CategoryIds = categoryIds?.ToList() ?? [];
    }

    public string Name { get; private set; }

    public string Slug { get; private set; }

    public ProductKind Kind { get; private set; }

    public bool IsPublished { get; private set; }

    public IReadOnlyList<string> CategoryIds { get; private set; }

    public IReadOnlyList<ProductVariant> Variants => _variants;

    /// <summary>«شناسهٔ کالا/خدمت» مؤدیان — رزرو فاز ۱۱.</summary>
    public string? TaxpayerGoodsCode { get; private set; }

    public Money LowestPrice => _variants.Min(v => v.Price);

    public void Publish()
    {
        IsPublished = true;
        Touch();
    }

    public void Unpublish()
    {
        IsPublished = false;
        Touch();
    }

    public ProductVariant GetVariant(string variantId) =>
        _variants.FirstOrDefault(v => v.Id == variantId)
        ?? throw new KeyNotFoundException($"Variant '{variantId}' not found on product '{Id}'.");

    public void SetTaxpayerGoodsCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        TaxpayerGoodsCode = code;
        Touch();
    }
}

/// <summary>
/// گونهٔ محصول (رنگ/سایز). موجودی و قیمت اینجا زندگی می‌کنند.
/// <see cref="StockOnHand"/> فقط از طریق عملیات Atomic لایهٔ زیرساخت تغییر می‌کند.
/// </summary>
public sealed class ProductVariant
{
    public ProductVariant(
        string id,
        string sku,
        Money price,
        int stockOnHand,
        IReadOnlyDictionary<string, string>? attributes = null,
        bool trackInventory = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);

        if (price.IsNegative)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Price cannot be negative.");
        }

        if (stockOnHand < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stockOnHand), "Stock cannot be negative.");
        }

        Id = id;
        Sku = sku;
        Price = price;
        StockOnHand = stockOnHand;
        TrackInventory = trackInventory;
        Attributes = attributes ?? new Dictionary<string, string>();
    }

    public string Id { get; private set; }

    public string Sku { get; private set; }

    public Money Price { get; private set; }

    public int StockOnHand { get; private set; }

    public bool TrackInventory { get; private set; }

    /// <summary>مثل { "color": "قرمز", "size": "L" }.</summary>
    public IReadOnlyDictionary<string, string> Attributes { get; private set; }

    public bool IsAvailable(int quantity) => !TrackInventory || StockOnHand >= quantity;

    /// <summary>نام نمایشی گونه از روی صفات.</summary>
    public string DisplayName => Attributes.Count == 0
        ? Sku
        : string.Join(" / ", Attributes.Select(a => a.Value));
}
