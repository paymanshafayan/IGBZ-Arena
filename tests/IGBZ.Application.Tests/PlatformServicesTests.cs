using FluentAssertions;
using IGBZ.Domain.Common;
using IGBZ.Domain.Integration;
using IGBZ.Domain.Instagram;
using IGBZ.Domain.Tenancy;
using IGBZ.Domain.Lms;
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

    [Fact]
    public void IntegrationConnection_should_initialize_correctly()
    {
        var id = "conn-1";
        var tenantId = new TenantId("shop-x");
        var connection = new IntegrationConnection(
            id,
            tenantId,
            "Digikala",
            IntegrationType.Marketplace,
            "secret_api_key_123",
            new Dictionary<string, string> { { "sync_interval", "30" } });

        connection.Id.Should().Be(id);
        connection.TenantId.Should().Be(tenantId.Value);
        connection.ProviderName.Should().Be("Digikala");
        connection.Type.Should().Be(IntegrationType.Marketplace);
        connection.ApiKey.Should().Be("secret_api_key_123");
        connection.IsConnected.Should().BeTrue();
        connection.Settings["sync_interval"].Should().Be("30");

        connection.Disconnect();
        connection.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void InstagramCampaign_should_initialize_correctly()
    {
        var id = "camp-1";
        var tenantId = new TenantId("shop-x");
        var campaign = new InstagramCampaign(
            id,
            tenantId,
            "کمپین استوری",
            CampaignKind.MentionStory,
            "قیمت",
            "سلام! قیمت به دایرکت ارسال شد",
            "COUPON15");

        campaign.Id.Should().Be(id);
        campaign.TenantId.Should().Be(tenantId.Value);
        campaign.Title.Should().Be("کمپین استوری");
        campaign.Kind.Should().Be(CampaignKind.MentionStory);
        campaign.Keyword.Should().Be("قیمت");
        campaign.ResponseTemplate.Should().Be("سلام! قیمت به دایرکت ارسال شد");
        campaign.CouponCodeToAttach.Should().Be("COUPON15");
        campaign.IsActive.Should().BeTrue();

        campaign.Deactivate();
        campaign.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Course_should_initialize_and_add_lessons()
    {
        var id = "course-123";
        var tenantId = new TenantId("shop-x");
        var course = new Course(id, tenantId, "prod-456", "دوره پیشرفته لاراول");

        course.Id.Should().Be(id);
        course.TenantId.Should().Be(tenantId.Value);
        course.ProductId.Should().Be("prod-456");
        course.Title.Should().Be("دوره پیشرفته لاراول");
        course.Lessons.Should().BeEmpty();

        var lesson = new CourseLesson("les-1", "مقدمات", "https://stream.arvancloud.ir/hls/laravel/1", 45);
        course.AddLesson(lesson);

        course.Lessons.Should().HaveCount(1);
        course.Lessons[0].Id.Should().Be("les-1");
        course.Lessons[0].Title.Should().Be("مقدمات");
        course.Lessons[0].VideoHlsUrl.Should().Be("https://stream.arvancloud.ir/hls/laravel/1");
        course.Lessons[0].DurationMinutes.Should().Be(45);
    }
}
