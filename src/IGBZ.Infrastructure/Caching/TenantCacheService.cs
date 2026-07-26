using System.Collections.Concurrent;
using IGBZ.Application.Abstractions;

namespace IGBZ.Infrastructure.Caching;

/// <summary>
/// پیاده‌سازی سرویس کش چندمستأجری ایمن با پیشوندگذاری خودکار کلیدهای کش با شناسه‌ی مستأجر جاری (فاز ۶ - بخش ۲).
/// </summary>
public sealed class TenantCacheService(ITenantContext tenantContext) : ITenantCacheService
{
    private static readonly ConcurrentDictionary<string, (object Value, DateTimeOffset Expires)> Cache = new(StringComparer.Ordinal);

    private string GetScopedKey(string key) => $"{tenantContext.Current.Value}:{key}";

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var scopedKey = GetScopedKey(key);
        if (Cache.TryGetValue(scopedKey, out var entry))
        {
            if (DateTimeOffset.UtcNow < entry.Expires)
            {
                return Task.FromResult((T?)entry.Value);
            }
            Cache.TryRemove(scopedKey, out _);
        }

        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan absoluteExpiration, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        var scopedKey = GetScopedKey(key);
        var expires = DateTimeOffset.UtcNow.Add(absoluteExpiration);
        Cache[scopedKey] = (value, expires);

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var scopedKey = GetScopedKey(key);
        Cache.TryRemove(scopedKey, out _);

        return Task.CompletedTask;
    }
}
