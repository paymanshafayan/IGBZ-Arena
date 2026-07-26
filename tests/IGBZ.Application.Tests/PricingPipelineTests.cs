using FluentAssertions;
using IGBZ.Application.Pricing;
using IGBZ.Domain.Common;
using IGBZ.Domain.Ordering;
using IGBZ.Domain.Pricing;
using IGBZ.Domain.Tenancy;
using Xunit;

namespace IGBZ.Application.Tests;

public class PricingPipelineTests
{
    private static readonly TenantId Shop = new("shop-a");
    private static readonly DateTimeOffset Now = new(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);

    private static readonly IOrderPricingPipeline Pipeline = new OrderPricingPipeline(
        new SubTotalCalculator(),
        new BestSingleDiscountCalculator(),
        new FlatVatCalculator(),
        new QuoteShippingCalculator());

    private static PricingRequest Request(
        IReadOnlyList<Discount>? discounts = null,
        string? coupon = null,
        ShippingQuote? shipping = null,
        TaxSettings? tax = null) =>
        new(
            Lines:
            [
                new OrderLine("p1", "v1", "تی‌شرت", "قرمز / L", new Money(100_000m), 2),
                new OrderLine("p2", "v2", "شلوار", "مشکی / 32", new Money(300_000m), 1),
            ],
            AvailableDiscounts: discounts ?? [],
            CouponCode: coupon,
            Shipping: shipping ?? new ShippingQuote(new Money(50_000m), "post"),
            Tax: tax ?? new TaxSettings(10m),
            NowUtc: Now);

    [Fact]
    public void Computes_subtotal_tax_and_grand_total()
    {
        var totals = Pipeline.Calculate(Request());

        totals.SubTotal.Amount.Should().Be(500_000m);
        totals.DiscountTotal.IsZero.Should().BeTrue();
        totals.TaxTotal.Amount.Should().Be(50_000m);       // ۱۰٪ از ۵۰۰٬۰۰۰
        totals.ShippingTotal.Amount.Should().Be(50_000m);
        totals.GrandTotal.Amount.Should().Be(600_000m);
    }

    [Fact]
    public void Tax_is_applied_after_discount()
    {
        var discount = new Discount("d1", Shop, "۲۰٪ تخفیف", DiscountType.Percentage, 20m);
        var totals = Pipeline.Calculate(Request([discount]));

        totals.DiscountTotal.Amount.Should().Be(100_000m);
        totals.TaxableBase.Amount.Should().Be(400_000m);
        totals.TaxTotal.Amount.Should().Be(40_000m);
        totals.GrandTotal.Amount.Should().Be(490_000m);    // 400k + 40k + 50k
    }

    [Fact]
    public void Coupon_is_ignored_when_code_is_not_supplied()
    {
        var coupon = new Discount("d1", Shop, "کوپن", DiscountType.FixedAmount, 60_000m, couponCode: "IGBZ10");

        Pipeline.Calculate(Request([coupon])).DiscountTotal.IsZero.Should().BeTrue();
        Pipeline.Calculate(Request([coupon], coupon: "igbz10")).DiscountTotal.Amount.Should().Be(60_000m);
    }

    [Fact]
    public void Only_one_discount_is_applied_level_two()
    {
        var a = new Discount("d1", Shop, "۱۰٪", DiscountType.Percentage, 10m);
        var b = new Discount("d2", Shop, "۳۰٪", DiscountType.Percentage, 30m);

        var totals = Pipeline.Calculate(Request([a, b]));

        totals.AppliedDiscounts.Should().ContainSingle();
        totals.DiscountTotal.Amount.Should().Be(150_000m);
    }

    [Fact]
    public void Higher_priority_wins_over_larger_amount()
    {
        var big = new Discount("d1", Shop, "۳۰٪", DiscountType.Percentage, 30m, priority: 0);
        var preferred = new Discount("d2", Shop, "۱۰٪ ویژه", DiscountType.Percentage, 10m, priority: 5);

        var totals = Pipeline.Calculate(Request([big, preferred]));

        totals.AppliedDiscounts.Single().DiscountId.Should().Be("d2");
    }

    [Fact]
    public void Discount_respects_maximum_cap()
    {
        var capped = new Discount(
            "d1", Shop, "۵۰٪ تا سقف ۸۰ هزار", DiscountType.Percentage, 50m,
            maximumDiscountAmount: new Money(80_000m));

        Pipeline.Calculate(Request([capped])).DiscountTotal.Amount.Should().Be(80_000m);
    }

    [Fact]
    public void Discount_respects_minimum_subtotal()
    {
        var gated = new Discount(
            "d1", Shop, "تخفیف خرید بالای یک میلیون", DiscountType.FixedAmount, 100_000m,
            minimumOrderSubTotal: new Money(1_000_000m));

        Pipeline.Calculate(Request([gated])).DiscountTotal.IsZero.Should().BeTrue();
    }

    [Fact]
    public void Expired_and_exhausted_discounts_are_skipped()
    {
        var expired = new Discount(
            "d1", Shop, "منقضی", DiscountType.Percentage, 20m,
            endsAtUtc: Now.AddDays(-1));

        var exhausted = new Discount(
            "d2", Shop, "تمام‌شده", DiscountType.Percentage, 20m, usageLimit: 1);
        exhausted.RecordUsage();

        Pipeline.Calculate(Request([expired, exhausted])).DiscountTotal.IsZero.Should().BeTrue();
    }

    [Fact]
    public void Discount_never_exceeds_subtotal()
    {
        var huge = new Discount("d1", Shop, "تخفیف بزرگ", DiscountType.FixedAmount, 9_999_999m);
        var totals = Pipeline.Calculate(Request([huge]));

        totals.DiscountTotal.Amount.Should().Be(500_000m);
        totals.TaxableBase.IsZero.Should().BeTrue();
        totals.TaxTotal.IsZero.Should().BeTrue();
        totals.GrandTotal.Amount.Should().Be(50_000m);     // فقط هزینهٔ ارسال
    }

    [Fact]
    public void Tax_inclusive_prices_extract_embedded_vat()
    {
        var totals = Pipeline.Calculate(Request(tax: new TaxSettings(10m, PricesIncludeTax: true)));

        // ۵۰۰٬۰۰۰ شامل مالیات → مالیات مستتر = 500000 * 10/110
        totals.TaxTotal.Amount.Should().Be(45_455m);
    }

    [Fact]
    public void Taxable_shipping_adds_its_own_vat()
    {
        var totals = Pipeline.Calculate(
            Request(shipping: new ShippingQuote(new Money(50_000m), "post", IsTaxable: true)));

        totals.TaxTotal.Amount.Should().Be(55_000m);       // 50k کالا + 5k ارسال
        totals.GrandTotal.Amount.Should().Be(605_000m);
    }

    [Fact]
    public void Empty_cart_produces_zero_totals()
    {
        var request = Request() with { Lines = [] };
        Pipeline.Calculate(request).GrandTotal.IsZero.Should().BeTrue();
    }
}
