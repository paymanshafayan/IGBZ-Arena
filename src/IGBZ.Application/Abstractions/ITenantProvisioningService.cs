using IGBZ.Domain.Tenancy;

namespace IGBZ.Application.Abstractions;

/// <summary>
/// سرویس آماده‌سازی و راه‌اندازی مستأجر جدید (فاز ۲).
/// </summary>
public interface ITenantProvisioningService
{
    Task<Tenant> ProvisionTenantAsync(
        string name,
        string subdomain,
        string adminPhoneNumber,
        CancellationToken cancellationToken = default);
}
