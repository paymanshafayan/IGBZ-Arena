using IGBZ.Domain.Common;

namespace IGBZ.Application.Abstractions;

/// <summary>
/// انتزاع درگاه پرداخت مشترک (فاز ۶).
/// </summary>
public interface IPaymentGatewayService
{
    Task<PaymentGatewayResult> StartPaymentAsync(
        string orderId,
        Money amount,
        string callbackUrl,
        CancellationToken cancellationToken = default);

    Task<PaymentVerificationResult> VerifyPaymentAsync(
        string orderId,
        Money amount,
        string authority,
        CancellationToken cancellationToken = default);
}

public sealed record PaymentGatewayResult(bool Succeeded, string? RedirectUrl, string? ErrorMessage);

public sealed record PaymentVerificationResult(bool Succeeded, string? TransactionId, string? ErrorMessage);
