using System.Collections.Concurrent;
using IGBZ.Application.Abstractions;

namespace IGBZ.Infrastructure.Security;

/// <summary>
/// پیاده‌سازی درون‌حافظه‌ای سرویس OTP برای توسعه و تسریع در آزمایشات (فاز ۲).
/// </summary>
public sealed class InMemoryOtpService : IOtpService
{
    private readonly ConcurrentDictionary<string, string> _otps = new(StringComparer.Ordinal);

    public Task<string> GenerateOtpAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);

        // تولید کد تستی ثابت ۱۲۳۴۵ برای تسریع در روند توسعه
        var code = "12345";
        _otps[phoneNumber] = code;
        return Task.FromResult(code);
    }

    public Task<bool> VerifyOtpAsync(string phoneNumber, string code, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        if (_otps.TryGetValue(phoneNumber, out var actual) && string.Equals(actual, code, StringComparison.Ordinal))
        {
            _otps.TryRemove(phoneNumber, out _);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }
}
