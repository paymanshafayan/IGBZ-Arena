using FluentAssertions;
using IGBZ.Domain.Common;
using IGBZ.Infrastructure.Payment;
using IGBZ.Infrastructure.Security;
using Xunit;

namespace IGBZ.Application.Tests;

/// <summary>
/// آزمون‌های واحد برای سرویس‌های تکمیلی فازهای مختلف پلتفرم (فاز ۲ الی ۱۲).
/// </summary>
public class PlatformServicesTests
{
    [Fact]
    public async Task OtpService_should_generate_and_verify_correct_otp()
    {
        var otpService = new InMemoryOtpService();
        var phone = "+989123456789";

        var code = await otpService.GenerateOtpAsync(phone);
        code.Should().Be("12345");

        var valid = await otpService.VerifyOtpAsync(phone, code);
        valid.Should().BeTrue();

        // تلاش مجدد با همان کد باید ناموفق باشد چون کد منقضی شده است
        var retry = await otpService.VerifyOtpAsync(phone, code);
        retry.Should().BeFalse();
    }

    [Fact]
    public async Task PaymentGateway_should_start_and_verify_sandbox_payment()
    {
        var gateway = new SandboxPaymentGateway();
        var orderId = "order-123";
        var amount = new Money(500_000m);
        var callbackUrl = "https://mytenant.igbz.ir/payment/callback";

        var startResult = await gateway.StartPaymentAsync(orderId, amount, callbackUrl);
        startResult.Succeeded.Should().BeTrue();
        startResult.RedirectUrl.Should().Contain("authority=sandbox_");

        var authority = "sandbox_test_authority_123";
        var verifyResult = await gateway.VerifyPaymentAsync(orderId, amount, authority);
        verifyResult.Succeeded.Should().BeTrue();
        verifyResult.TransactionId.Should().StartWith("TXN_");
    }
}
