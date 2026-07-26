using IGBZ.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace IGBZ.Infrastructure.Sms;

/// <summary>
/// پیاده‌سازی شبیه‌ساز سرویس پیامک کاوه‌نگار بر اساس وب‌سرویس پترن Lookup (فاز ۱ - بخش ۲).
/// </summary>
public sealed class KavenegarSmsService(ILogger<KavenegarSmsService> logger) : ISmsService
{
    public Task SendSmsAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        // شبیه‌سازی ارسال پیامک معمولی
        logger.LogInformation("Kavenegar SMS sent to {Phone}: {Message}", phoneNumber, message);
        return Task.CompletedTask;
    }

    public Task SendOtpSmsAsync(string phoneNumber, string code, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        // شبیه‌سازی فراخوانی وب‌سرویس ارسال پترن کاوه‌نگار: https://api.kavenegar.com/v1/.../verify/lookup.json
        logger.LogInformation(
            "Kavenegar Pattern Lookup invoked for {Phone}. Template: 'otp_verify', Token: '{Code}'",
            phoneNumber,
            code);

        return Task.CompletedTask;
    }
}
