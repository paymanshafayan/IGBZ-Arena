namespace IGBZ.Application.Abstractions;

/// <summary>
/// سرویس تولید توکن‌های امن چندمستأجری JWT (فاز ۱ - بخش ۲).
/// </summary>
public interface ITokenService
{
    string GenerateAccessToken(string userId, string tenantId, string phoneNumber, string role);
}
