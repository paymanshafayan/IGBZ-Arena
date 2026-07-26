using IGBZ.Domain.Tenancy;

namespace IGBZ.Application.Abstractions;

/// <summary>
/// مستأجر جاری درخواست. توسط Middleware از JWT Claim یا Host Header پر می‌شود.
/// هیچ Controller/Service اجازه ندارد <c>tenantId</c> را از ورودی کاربر بخواند.
/// </summary>
public interface ITenantContext
{
    TenantId Current { get; }

    bool IsResolved { get; }
}

/// <summary>
/// پیاده‌سازی قابل نوشتن که فقط Middleware اجازهٔ ست کردنش را دارد (Scoped per-request).
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private TenantId? _current;

    public TenantId Current => _current
        ?? throw new TenantNotResolvedException(
            "Tenant has not been resolved for this request. TenantResolutionMiddleware must run first.");

    public bool IsResolved => _current.HasValue;

    public void Set(TenantId tenantId)
    {
        if (_current.HasValue)
        {
            throw new InvalidOperationException("Tenant is already resolved for this request.");
        }

        _current = tenantId;
    }
}

public sealed class TenantNotResolvedException(string message) : InvalidOperationException(message);
