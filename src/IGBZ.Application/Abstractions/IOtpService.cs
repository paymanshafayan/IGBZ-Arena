namespace IGBZ.Application.Abstractions;

/// <summary>
/// سرویس مدیریت رمزهای یک‌بارمصرف (فاز ۲).
/// </summary>
public interface IOtpService
{
    Task<string> GenerateOtpAsync(string phoneNumber, CancellationToken cancellationToken = default);
    Task<bool> VerifyOtpAsync(string phoneNumber, string code, CancellationToken cancellationToken = default);
}
