using IGBZ.Application.Abstractions;
using IGBZ.Domain.Common;

namespace IGBZ.Infrastructure.Payment;

/// <summary>
/// پیاده‌سازی شبیه‌ساز (Sandbox) درگاه پرداخت برای تکمیل و آزمایش سناریو پرداخت پایه‌ای (فاز ۶).
/// </summary>
public sealed class SandboxPaymentGateway : IPaymentGatewayService
{
    public Task<PaymentGatewayResult> StartPaymentAsync(
        string orderId,
        Money amount,
        string callbackUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(callbackUrl);

        var redirectUrl = $"{callbackUrl}?authority=sandbox_{Guid.NewGuid():N}&status=OK";
        return Task.FromResult(new PaymentGatewayResult(true, redirectUrl, null));
    }

    public Task<PaymentVerificationResult> VerifyPaymentAsync(
        string orderId,
        Money amount,
        string authority,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(authority);

        if (authority.StartsWith("sandbox_", StringComparison.Ordinal))
        {
            var transactionId = $"TXN_{Guid.NewGuid():N}";
            return Task.FromResult(new PaymentVerificationResult(true, transactionId, null));
        }

        return Task.FromResult(new PaymentVerificationResult(false, null, "Invalid payment authority."));
    }
}
