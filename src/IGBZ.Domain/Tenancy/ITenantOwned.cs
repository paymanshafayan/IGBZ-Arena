namespace IGBZ.Domain.Tenancy;

/// <summary>
/// هر موجودیتی که به یک مستأجر تعلق دارد باید این را پیاده کند.
/// <c>ITenantScopedRepository&lt;T&gt;</c> فقط روی <see cref="ITenantOwned"/> قید دارد،
/// پس هیچ Collection تننتی نمی‌تواند بدون <c>tenantId</c> از ریپازیتوری استفاده کند.
/// </summary>
public interface ITenantOwned
{
    string TenantId { get; }
}

/// <summary>پایهٔ مشترک همهٔ موجودیت‌های تننت‌دار.</summary>
public abstract class TenantEntity : ITenantOwned
{
    protected TenantEntity(string id, TenantId tenantId)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Id is required.", nameof(id));
        }

        Id = id;
        TenantId = tenantId.Value;
    }

    public string Id { get; private set; }

    public string TenantId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; protected set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAtUtc { get; protected set; }

    protected void Touch() => UpdatedAtUtc = DateTimeOffset.UtcNow;
}
