using System.Linq.Expressions;
using IGBZ.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace IGBZ.Infrastructure.BackgroundJobs;

/// <summary>
/// پیاده‌سازی و شبیه‌ساز صف پس‌زمینه Hangfire بر بستر مونو‌دی‌بی (فاز ۱ - بخش ۳).
/// </summary>
public sealed class HangfireBackgroundJobQueue(ILogger<HangfireBackgroundJobQueue> logger) : IBackgroundJobQueue
{
    public string Enqueue(Expression<Action> methodCall)
    {
        ArgumentNullException.ThrowIfNull(methodCall);

        var jobId = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        logger.LogInformation("Background Job Enqueued: ID: {JobId}, Call: {Call}", jobId, methodCall.ToString());

        // شبیه‌سازی اجرای غیرهم‌زمان وظیفه در پس‌زمینه
        Task.Run(() =>
        {
            try
            {
                var action = methodCall.Compile();
                action();
                logger.LogInformation("Background Job {JobId} completed successfully.", jobId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background Job {JobId} failed.", jobId);
            }
        });

        return jobId;
    }

    public string Enqueue<T>(Expression<Action<T>> methodCall)
    {
        ArgumentNullException.ThrowIfNull(methodCall);

        var jobId = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        logger.LogInformation("Generic Background Job Enqueued: ID: {JobId}, Call: {Call}", jobId, methodCall.ToString());
        return jobId;
    }
}
