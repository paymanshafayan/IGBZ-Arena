using IGBZ.Domain.Common;

namespace IGBZ.Domain.Ordering;

/// <summary>
/// یک قلم سفارش. همیشه به یک Variant مشخص اشاره دارد (بخش ۵.۴: موجودی Variant-based).
/// عنوان/قیمت در لحظهٔ ثبت سفارش کپی می‌شود (Snapshot) تا تغییرات بعدی کاتالوگ
/// فاکتور صادرشده را تغییر ندهد — الزام فاکتور مؤدیان.
/// </summary>
public sealed class OrderLine
{
    public OrderLine(
        string productId,
        string variantId,
        string productName,
        string? variantName,
        Money unitPrice,
        int quantity,
        string? taxCategoryCode = null,
        string? taxpayerGoodsCode = null)
    {
        if (string.IsNullOrWhiteSpace(productId))
        {
            throw new ArgumentException("ProductId is required.", nameof(productId));
        }

        if (string.IsNullOrWhiteSpace(variantId))
        {
            throw new ArgumentException("VariantId is required.", nameof(variantId));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        if (unitPrice.IsNegative)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price cannot be negative.");
        }

        ProductId = productId;
        VariantId = variantId;
        ProductName = productName;
        VariantName = variantName;
        UnitPrice = unitPrice;
        Quantity = quantity;
        TaxCategoryCode = taxCategoryCode;
        TaxpayerGoodsCode = taxpayerGoodsCode;
    }

    public string ProductId { get; }

    public string VariantId { get; }

    public string ProductName { get; }

    public string? VariantName { get; }

    public Money UnitPrice { get; }

    public int Quantity { get; }

    /// <summary>دستهٔ مالیاتی (رزرو برای نرخ‌های معاف/صفر).</summary>
    public string? TaxCategoryCode { get; }

    /// <summary>«شناسهٔ کالا/خدمت» سامانهٔ مؤدیان — رزروشده برای فاز ۱۱.</summary>
    public string? TaxpayerGoodsCode { get; }

    public Money LineTotal => UnitPrice * Quantity;
}
