using System.Collections.Concurrent;
using System.Linq.Expressions;
using IGBZ.Application.Abstractions;
using IGBZ.Domain.Tenancy;

namespace IGBZ.Infrastructure.Tenancy;

/// <summary>
/// پیادهٔ درون‌حافظه‌ای با همان قرارداد امنیتی <see cref="ITenantScopedRepository{T}"/>.
/// کاربرد: تست‌های جداسازی چندمستأجری و توسعهٔ محلی بدون MongoDB.
/// ذخیره‌سازی مشترک (<paramref name="store"/>) عمداً بین مستأجرها یکی است تا نشتی
/// واقعاً قابل تشخیص باشد.
/// </summary>
public sealed class InMemoryTenantScopedRepository<T>(
    ConcurrentDictionary<string, T> store,
    ITenantContext tenantContext) : ITenantScopedRepository<T>
    where T : class, ITenantOwned
{
    private string CurrentTenant => tenantContext.Current.Value;

    private IEnumerable<T> Scoped =>
        store.Values.Where(x => string.Equals(x.TenantId, CurrentTenant, StringComparison.Ordinal));

    public Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var found = store.TryGetValue(id, out var entity)
            && string.Equals(entity.TenantId, CurrentTenant, StringComparison.Ordinal)
                ? entity
                : null;

        return Task.FromResult(found);
    }

    public Task<IReadOnlyList<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        IReadOnlyList<T> result = [.. Scoped.Where(predicate.Compile())];
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<T>> ListAsync(
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<T> result = [.. Scoped.Skip(skip).Take(take)];
        return Task.FromResult(result);
    }

    public Task<long> CountAsync(
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        var query = predicate is null ? Scoped : Scoped.Where(predicate.Compile());
        return Task.FromResult(query.LongCount());
    }

    public Task InsertAsync(T entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureBelongsToCurrentTenant(entity);
        store[GetId(entity)] = entity;
        return Task.CompletedTask;
    }

    public Task<bool> ReplaceAsync(T entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureBelongsToCurrentTenant(entity);

        var id = GetId(entity);
        if (!store.TryGetValue(id, out var existing) ||
            !string.Equals(existing.TenantId, CurrentTenant, StringComparison.Ordinal))
        {
            return Task.FromResult(false);
        }

        store[id] = entity;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (!store.TryGetValue(id, out var existing) ||
            !string.Equals(existing.TenantId, CurrentTenant, StringComparison.Ordinal))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(store.TryRemove(id, out _));
    }

    private void EnsureBelongsToCurrentTenant(T entity)
    {
        if (!string.Equals(entity.TenantId, CurrentTenant, StringComparison.Ordinal))
        {
            throw new CrossTenantAccessException(
                $"Entity of type '{typeof(T).Name}' belongs to tenant '{entity.TenantId}' " +
                $"but the current tenant is '{CurrentTenant}'.");
        }
    }

    private static string GetId(T entity)
    {
        var property = typeof(T).GetProperty("Id")
            ?? throw new InvalidOperationException($"Type '{typeof(T).Name}' has no Id property.");

        return property.GetValue(entity) as string
            ?? throw new InvalidOperationException($"Entity of type '{typeof(T).Name}' has an empty Id.");
    }
}
