namespace IGBZ.Application.Abstractions;

/// <summary>
/// سرویس ارسال پیامک پلتفرم متصل به پنل‌های ایرانی (فاز ۱ - بخش ۲).
/// </summary>
public interface ISmsService
{
    Task SendSmsAsync(string phoneNumber, string message, CancellationToken cancellationToken = default);
    Task SendOtpSmsAsync(string phoneNumber, string code, CancellationToken cancellationToken = default);
}
