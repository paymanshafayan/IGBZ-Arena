using FluentAssertions;
using IGBZ.Domain.Common;
using IGBZ.Domain.Ordering;
using IGBZ.Domain.Tenancy;
using Xunit;

namespace IGBZ.Domain.Tests;

public class OrderStateMachineTests
{
    private static Order NewOrder() => new(
        "ord-1",
        new TenantId("shop-a"),
        "IGBZ-1001",
        "cust-1",
        [new OrderLine("p1", "v1", "تی‌شرت", "قرمز / L", new Money(250_000m), 2)]);

    [Fact]
    public void New_order_starts_pending()
    {
        var order = NewOrder();
        order.Status.Should().Be(OrderStatus.Pending);
        order.History.Should().ContainSingle();
    }

    [Fact]
    public void Happy_path_reaches_delivered()
    {
        var order = NewOrder();

        order.MarkAsPaid("tx-1");
        order.StartProcessing();
        order.MarkAsShipped("TRK-9");
        order.MarkAsDelivered();

        order.Status.Should().Be(OrderStatus.Delivered);
        order.PaymentTransactionId.Should().Be("tx-1");
        order.TrackingCode.Should().Be("TRK-9");
        order.History.Should().HaveCount(5);
    }

    [Fact]
    public void Cannot_ship_before_payment()
    {
        var order = NewOrder();
        var act = () => order.MarkAsShipped("TRK-1");
        act.Should().Throw<InvalidOrderTransitionException>();
    }

    [Fact]
    public void Cancelled_order_is_terminal()
    {
        var order = NewOrder();
        order.Cancel("مشتری منصرف شد");

        order.Status.Should().Be(OrderStatus.Cancelled);
        order.CancellationReason.Should().Be("مشتری منصرف شد");
        OrderStatusTransitions.IsTerminal(OrderStatus.Cancelled).Should().BeTrue();

        var act = () => order.MarkAsPaid("tx-2");
        act.Should().Throw<InvalidOrderTransitionException>();
    }

    [Fact]
    public void Cancel_requires_a_reason()
    {
        var order = NewOrder();
        var act = () => order.Cancel("  ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Delivered_order_can_be_returned_then_refunded()
    {
        var order = NewOrder();
        order.MarkAsPaid("tx-1");
        order.StartProcessing();
        order.MarkAsShipped("TRK-9");
        order.MarkAsDelivered();
        order.MarkAsReturned("کالای معیوب");
        order.MarkAsRefunded("بازگشت وجه");

        order.Status.Should().Be(OrderStatus.Refunded);
    }

    [Fact]
    public void Totals_cannot_change_after_payment()
    {
        var order = NewOrder();
        order.MarkAsPaid("tx-1");

        var act = () => order.ApplyTotals(OrderTotals.Empty());
        act.Should().Throw<InvalidOrderTransitionException>();
    }

    [Fact]
    public void Order_requires_at_least_one_line()
    {
        var act = () => new Order("o", new TenantId("shop-a"), "N-1", "c1", []);
        act.Should().Throw<ArgumentException>();
    }
}
