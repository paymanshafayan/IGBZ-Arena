using IGBZ.Application.Abstractions;
using IGBZ.Domain.Tenancy;
using IGBZ.Infrastructure.Mongo;
using MongoDB.Driver;

namespace IGBZ.Infrastructure.Tenancy;

/// <summary>
/// سرویس تامین مستأجران روی مونو‌دی‌بی واقعی (فاز ۲).
/// </summary>
public sealed class TenantProvisioningService : ITenantProvisioningService
{
    private readonly IMongoCollection<Tenant> _tenants;

    public TenantProvisioningService(IMongoDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _tenants = database.GetCollection<Tenant>(MongoCollections.PlatformTenants);
    }

    public async Task<Tenant> ProvisionTenantAsync(
        string name,
        string subdomain,
        string adminPhoneNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(subdomain);
        ArgumentException.ThrowIfNullOrWhiteSpace(adminPhoneNumber);

        // تولید شناسه منحصر‌به‌فرد برای تننت جدید
        var tenantId = $"shop-{Guid.NewGuid():N}";

        // ساخت موجودیت تننت جدید تحت پلن پایه "standard"
        var tenant = new Tenant(tenantId, name, subdomain, "standard");
        tenant.Activate();

        await _tenants.InsertOneAsync(tenant, options: null, cancellationToken).ConfigureAwait(false);

        return tenant;
    }
}
