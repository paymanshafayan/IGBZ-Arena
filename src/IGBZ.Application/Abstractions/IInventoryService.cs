namespace IGBZ.Application.Abstractions;

/// <summary>
/// رزرو موجودی (بخش ۵.۴). پیاده‌سازی موظف است Atomic باشد
/// (<c>findOneAndUpdate</c> با شرط <c>stock &gt;= qty</c>) تا دو خریدار هم‌زمان
/// آخرین واحد را نگیرند.
/// </summary>
public interface IInventoryService
{
    Task<InventoryReservationResult> TryReserveAsync(
        IReadOnlyList<InventoryReservationRequest> items,
        CancellationToken cancellationToken = default);

    Task ReleaseAsync(
        IReadOnlyList<InventoryReservationRequest> items,
        CancellationToken cancellationToken = default);
}

public sealed record InventoryReservationRequest
{
    public InventoryReservationRequest(string productId, string variantId, int quantity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        ArgumentException.ThrowIfNullOrWhiteSpace(variantId);

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        ProductId = productId;
        VariantId = variantId;
        Quantity = quantity;
    }

    public string ProductId { get; init; }

    public string VariantId { get; init; }

    public int Quantity { get; init; }
}

public sealed record InventoryReservationResult(
    bool Succeeded,
    IReadOnlyList<InventoryReservationRequest> Reserved,
    IReadOnlyList<InventoryShortage> Shortages)
{
    public static InventoryReservationResult Success(IReadOnlyList<InventoryReservationRequest> reserved) =>
        new(true, reserved, []);

    public static InventoryReservationResult Failure(IReadOnlyList<InventoryShortage> shortages) =>
        new(false, [], shortages);
}

public sealed record InventoryShortage(string ProductId, string VariantId, int Requested, int Available);
