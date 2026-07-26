using System.Linq.Expressions;
using IGBZ.Application.Abstractions;
using IGBZ.Domain.Tenancy;
using MongoDB.Driver;

namespace IGBZ.Infrastructure.Mongo;

/// <summary>
/// پیادهٔ MongoDB برای <see cref="ITenantScopedRepository{T}"/>.
///
/// تضمین کلیدی: تنها نقطه‌ای که <c>FilterDefinition</c> ساخته می‌شود
/// <see cref="ScopedFilter"/> است و همیشه <c>tenantId</c> را با AND اضافه می‌کند.
/// هیچ متد عمومی‌ای وجود ندارد که فیلتر خام بدون این قید اجرا کند.
/// </summary>
public class MongoTenantScopedRepository<T> : ITenantScopedRepository<T>
    where T : class, ITenantOwned
{
    private readonly ITenantContext _tenantContext;

    public MongoTenantScopedRepository(IMongoDatabase database, ITenantContext tenantContext, string collectionName)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);

        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        Collection = database.GetCollection<T>(collectionName);
    }

    protected IMongoCollection<T> Collection { get; }

    protected TenantId CurrentTenant => _tenantContext.Current;

    protected FilterDefinition<T> TenantFilter =>
        Builders<T>.Filter.Eq(x => x.TenantId, CurrentTenant.Value);

    protected FilterDefinition<T> ScopedFilter(FilterDefinition<T>? extra = null) =>
        extra is null ? TenantFilter : Builders<T>.Filter.And(TenantFilter, extra);

    protected FilterDefinition<T> ScopedFilter(Expression<Func<T, bool>> predicate) =>
        ScopedFilter(Builders<T>.Filter.Where(predicate));

    private static FilterDefinition<T> ById(string id) =>
        Builders<T>.Filter.Eq("_id", id);

    public virtual async Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return await Collection
            .Find(ScopedFilter(ById(id)))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public virtual async Task<IReadOnlyList<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        return await Collection
            .Find(ScopedFilter(predicate))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public virtual async Task<IReadOnlyList<T>> ListAsync(
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        if (skip < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(skip));
        }

        if (take is <= 0 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(take), "Take must be between 1 and 500.");
        }

        return await Collection
            .Find(ScopedFilter())
            .Skip(skip)
            .Limit(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public virtual async Task<long> CountAsync(
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        var filter = predicate is null ? ScopedFilter() : ScopedFilter(predicate);
        return await Collection
            .CountDocumentsAsync(filter, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public virtual async Task InsertAsync(T entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureBelongsToCurrentTenant(entity);

        await Collection
            .InsertOneAsync(entity, options: null, cancellationToken)
            .ConfigureAwait(false);
    }

    public virtual async Task<bool> ReplaceAsync(T entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureBelongsToCurrentTenant(entity);

        var id = IdAccessor<T>.GetId(entity);
        var result = await Collection
            .ReplaceOneAsync(ScopedFilter(ById(id)), entity, options: new ReplaceOptions(), cancellationToken)
            .ConfigureAwait(false);

        return result.MatchedCount > 0;
    }

    public virtual async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var result = await Collection
            .DeleteOneAsync(ScopedFilter(ById(id)), cancellationToken)
            .ConfigureAwait(false);

        return result.DeletedCount > 0;
    }

    /// <summary>
    /// دفاع عمقی: حتی اگر کدی موجودیتی با tenantId دیگر بسازد، نوشتنش رد می‌شود.
    /// </summary>
    protected void EnsureBelongsToCurrentTenant(T entity)
    {
        if (!string.Equals(entity.TenantId, CurrentTenant.Value, StringComparison.Ordinal))
        {
            throw new CrossTenantAccessException(
                $"Entity of type '{typeof(T).Name}' belongs to tenant '{entity.TenantId}' " +
                $"but the current tenant is '{CurrentTenant.Value}'.");
        }
    }
}
