namespace IGBZ.Domain.Ordering;

/// <summary>وضعیت‌های سفارش طبق ماشین‌حالت بخش ۵.۱ سند معماری.</summary>
public enum OrderStatus
{
    Pending = 0,
    Paid = 1,
    Processing = 2,
    Shipped = 3,
    Delivered = 4,
    Cancelled = 5,
    Returned = 6,
    Refunded = 7,
}

/// <summary>
/// جدول انتقال‌های مجاز. تنها مرجع مجاز بودن یک انتقال؛ هیچ‌جای دیگری
/// نباید <c>Status</c> را مستقیم تغییر دهد.
/// </summary>
public static class OrderStatusTransitions
{
    private static readonly IReadOnlyDictionary<OrderStatus, OrderStatus[]> Allowed =
        new Dictionary<OrderStatus, OrderStatus[]>
        {
            [OrderStatus.Pending] = [OrderStatus.Paid, OrderStatus.Cancelled],
            [OrderStatus.Paid] = [OrderStatus.Processing, OrderStatus.Cancelled, OrderStatus.Refunded],
            [OrderStatus.Processing] = [OrderStatus.Shipped, OrderStatus.Cancelled],
            [OrderStatus.Shipped] = [OrderStatus.Delivered, OrderStatus.Returned],
            [OrderStatus.Delivered] = [OrderStatus.Returned],
            [OrderStatus.Returned] = [OrderStatus.Refunded],
            [OrderStatus.Cancelled] = [],
            [OrderStatus.Refunded] = [],
        };

    public static bool CanTransition(OrderStatus from, OrderStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    public static IReadOnlyCollection<OrderStatus> NextStates(OrderStatus from) =>
        Allowed.TryGetValue(from, out var targets) ? targets : [];

    public static bool IsTerminal(OrderStatus status) => NextStates(status).Count == 0;
}
