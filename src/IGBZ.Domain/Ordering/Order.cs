using IGBZ.Domain.Common;
using IGBZ.Domain.Tenancy;

namespace IGBZ.Domain.Ordering;

/// <summary>
/// Aggregate Root سفارش. طبق بخش ۵.۱: تغییر وضعیت فقط از طریق متدهای صریح.
/// هیچ setter عمومی روی <see cref="Status"/> وجود ندارد.
/// </summary>
public sealed class Order : TenantEntity
{
    private readonly List<OrderLine> _lines;
    private readonly List<OrderStatusChange> _history = [];

    public Order(
        string id,
        TenantId tenantId,
        string orderNumber,
        string customerId,
        IEnumerable<OrderLine> lines,
        string currency = Money.DefaultCurrency)
        : base(id, tenantId)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            throw new ArgumentException("OrderNumber is required.", nameof(orderNumber));
        }

        if (string.IsNullOrWhiteSpace(customerId))
        {
            throw new ArgumentException("CustomerId is required.", nameof(customerId));
        }

        _lines = [.. lines];

        if (_lines.Count == 0)
        {
            throw new ArgumentException("An order must contain at least one line.", nameof(lines));
        }

        OrderNumber = orderNumber;
        CustomerId = customerId;
        Currency = currency;
        Totals = OrderTotals.Empty(currency);
        Status = OrderStatus.Pending;
        _history.Add(new OrderStatusChange(null, OrderStatus.Pending, DateTimeOffset.UtcNow, "created"));
    }

    public string OrderNumber { get; private set; }

    public string CustomerId { get; private set; }

    public string Currency { get; private set; }

    public OrderStatus Status { get; private set; }

    public OrderTotals Totals { get; private set; }

    public IReadOnlyList<OrderLine> Lines => _lines;

    public IReadOnlyList<OrderStatusChange> History => _history;

    public string? PaymentTransactionId { get; private set; }

    public string? CancellationReason { get; private set; }

    public string? TrackingCode { get; private set; }

    /// <summary>شمارهٔ منحصربه‌فرد مالیاتی (سامانهٔ مؤدیان) — رزرو فاز ۱۱.</summary>
    public string? TaxpayerInvoiceId { get; private set; }

    /// <summary>ثبت نتیجهٔ پایپ‌لاین محاسبه. فقط تا قبل از پرداخت مجاز است.</summary>
    public void ApplyTotals(OrderTotals totals)
    {
        if (Status != OrderStatus.Pending)
        {
            throw new InvalidOrderTransitionException(
                $"Totals can only be recalculated while the order is Pending (current: {Status}).");
        }

        if (totals.GrandTotal.Currency != Currency)
        {
            throw new InvalidOperationException("Totals currency does not match the order currency.");
        }

        Totals = totals;
        Touch();
    }

    public void MarkAsPaid(string paymentTransactionId)
    {
        if (string.IsNullOrWhiteSpace(paymentTransactionId))
        {
            throw new ArgumentException("Payment transaction id is required.", nameof(paymentTransactionId));
        }

        Transition(OrderStatus.Paid, $"payment:{paymentTransactionId}");
        PaymentTransactionId = paymentTransactionId;
    }

    public void StartProcessing() => Transition(OrderStatus.Processing, null);

    public void MarkAsShipped(string trackingCode)
    {
        if (string.IsNullOrWhiteSpace(trackingCode))
        {
            throw new ArgumentException("Tracking code is required.", nameof(trackingCode));
        }

        Transition(OrderStatus.Shipped, $"tracking:{trackingCode}");
        TrackingCode = trackingCode;
    }

    public void MarkAsDelivered() => Transition(OrderStatus.Delivered, null);

    public void Cancel(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A cancellation reason is required.", nameof(reason));
        }

        Transition(OrderStatus.Cancelled, reason);
        CancellationReason = reason;
    }

    public void MarkAsReturned(string reason) => Transition(OrderStatus.Returned, reason);

    public void MarkAsRefunded(string reason) => Transition(OrderStatus.Refunded, reason);

    public void AttachTaxpayerInvoiceId(string taxpayerInvoiceId)
    {
        if (string.IsNullOrWhiteSpace(taxpayerInvoiceId))
        {
            throw new ArgumentException("Taxpayer invoice id is required.", nameof(taxpayerInvoiceId));
        }

        TaxpayerInvoiceId = taxpayerInvoiceId;
        Touch();
    }

    private void Transition(OrderStatus target, string? note)
    {
        if (!OrderStatusTransitions.CanTransition(Status, target))
        {
            throw new InvalidOrderTransitionException(
                $"Transition {Status} → {target} is not allowed for order '{OrderNumber}'.");
        }

        var previous = Status;
        Status = target;
        _history.Add(new OrderStatusChange(previous, target, DateTimeOffset.UtcNow, note));
        Touch();
    }
}

public sealed record OrderStatusChange(
    OrderStatus? From,
    OrderStatus To,
    DateTimeOffset AtUtc,
    string? Note);

public sealed class InvalidOrderTransitionException(string message) : InvalidOperationException(message);
