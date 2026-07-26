using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IGBZ.Application.Abstractions;
using Microsoft.IdentityModel.Tokens;

namespace IGBZ.Infrastructure.Security;

/// <summary>
/// پیاده‌سازی سرویس امن تولید توکن چندمستأجری JWT (فاز ۱ - بخش ۲).
/// کلیدواژهٔ مستأجر جاری در Claim توکن تزریق می‌شود تا توسط Middleware برای جداسازی داده‌ها استفاده شود.
/// </summary>
public sealed class JwtTokenService : ITokenService
{
    private const string SecretKey = "igbz_super_secure_secret_key_for_jwt_token_generation_2026";

    public string GenerateAccessToken(string userId, string tenantId, string phoneNumber, string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(SecretKey);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim("tenantId", tenantId),
                new Claim(ClaimTypes.MobilePhone, phoneNumber),
                new Claim(ClaimTypes.Role, role)
            ]),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
