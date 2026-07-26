namespace IGBZ.Application.Abstractions;

/// <summary>
/// سرویس کش چندمستأجری برای ایمن‌سازی پلتفرم در برابر نشت اطلاعات بین فروشگاه‌ها (فاز ۶ - بخش ۲).
/// </summary>
public interface ITenantCacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan absoluteExpiration, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
