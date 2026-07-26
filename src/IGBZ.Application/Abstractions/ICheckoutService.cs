using IGBZ.Domain.Ordering;

namespace IGBZ.Application.Abstractions;

/// <summary>
/// سرویس تسویه‌حساب (گام ۲).
/// رزرو موجودی و ثبت سفارش را در یک تراکنش واحد و اتمی انجام می‌دهد.
/// </summary>
public interface ICheckoutService
{
    Task<CheckoutResult> CheckoutAsync(
        Order order,
        CancellationToken cancellationToken = default);
}

public sealed record CheckoutResult
{
    private CheckoutResult(bool succeeded, string? errorMessage, IReadOnlyList<InventoryShortage> shortages)
    {
        Succeeded = succeeded;
        ErrorMessage = errorMessage;
        Shortages = shortages;
    }

    public bool Succeeded { get; }

    public string? ErrorMessage { get; }

    public IReadOnlyList<InventoryShortage> Shortages { get; }

    public static CheckoutResult Success() => new(true, null, []);

    public static CheckoutResult Failure(string errorMessage) => new(false, errorMessage, []);

    public static CheckoutResult InventoryFailure(IReadOnlyList<InventoryShortage> shortages) =>
        new(false, "Inventory shortage.", shortages);
}
