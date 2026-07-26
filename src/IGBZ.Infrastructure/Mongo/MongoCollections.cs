namespace IGBZ.Infrastructure.Mongo;

/// <summary>
/// نام Collectionها در یک جا. Collectionهای سطح پلتفرم با <c>Platform</c> علامت‌گذاری
/// شده‌اند — این‌ها تنها موارد مجاز بدون <c>tenantId</c> هستند (بخش ۴).
/// </summary>
public static class MongoCollections
{
    // ----- تننت‌دار (همه دارای tenantId اجباری) -----
    public const string Products = "products";
    public const string Categories = "categories";
    public const string Orders = "orders";
    public const string Customers = "customers";
    public const string Discounts = "discounts";
    public const string Carts = "carts";
    public const string IntegrationConnections = "integration_connections";

    // ----- سطح پلتفرم (بدون tenantId) -----
    public const string PlatformTenants = "platform_tenants";
    public const string PlatformTenantPlans = "platform_tenant_plans";
    public const string PlatformDomainMappings = "platform_domain_mappings";
    public const string PlatformJobs = "platform_jobs";

    public static readonly IReadOnlySet<string> PlatformLevel = new HashSet<string>(StringComparer.Ordinal)
    {
        PlatformTenants,
        PlatformTenantPlans,
        PlatformDomainMappings,
        PlatformJobs,
    };
}
