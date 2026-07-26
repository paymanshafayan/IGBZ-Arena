using System.Globalization;
using FluentAssertions;
using IGBZ.Domain.Common;
using IGBZ.Domain.Tenancy;
using Xunit;

namespace IGBZ.Domain.Tests;

public class MoneyTests
{
    [Fact]
    public void Adding_different_currencies_throws()
    {
        var act = () => new Money(10m, "IRR") + new Money(10m, "USD");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Clamp_to_zero_prevents_negative_totals()
    {
        (new Money(100m) - new Money(250m)).ClampToZero().IsZero.Should().BeTrue();
    }

    [Theory]
    [InlineData("1234.4", "1234")]
    [InlineData("1234.5", "1235")]
    [InlineData("-1234.5", "-1235")]
    public void Rial_rounds_to_whole_units(string input, string expected)
    {
        var amount = decimal.Parse(input, CultureInfo.InvariantCulture);
        var target = decimal.Parse(expected, CultureInfo.InvariantCulture);
        new Money(amount).Round().Amount.Should().Be(target);
    }

    [Fact]
    public void Non_rial_currency_keeps_two_decimals()
    {
        new Money(10.005m, "USD").Round().Amount.Should().Be(10.01m);
    }

    [Fact]
    public void Min_returns_the_smaller_amount()
    {
        Money.Min(new Money(50m), new Money(30m)).Amount.Should().Be(30m);
    }
}

public class TenantIdTests
{
    [Theory]
    [InlineData("Shop-A", "shop-a")]
    [InlineData("  STORE1  ", "store1")]
    public void Normalizes_to_lowercase(string input, string expected)
    {
        new TenantId(input).Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("shop_a")]
    [InlineData("shop a")]
    [InlineData("shop.a")]
    [InlineData("shop/../a")]
    public void Rejects_invalid_values(string input)
    {
        var act = () => new TenantId(input);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Platform_is_recognized()
    {
        TenantId.Platform.IsPlatform.Should().BeTrue();
        new TenantId("shop-a").IsPlatform.Should().BeFalse();
    }
}
