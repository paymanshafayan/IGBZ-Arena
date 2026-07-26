using System.Linq.Expressions;

namespace IGBZ.Application.Abstractions;

/// <summary>
/// انتزاع صف کارهای پس‌زمینه (بخش ۸ و ADR-0003).
/// به ما اجازه می‌دهد کارها را بدون وابستگی مستقیم به Hangfire یا RabbitMQ، در پس‌زمینه صف‌بندی کنیم.
/// </summary>
public interface IBackgroundJobQueue
{
    string Enqueue(Expression<Action> methodCall);
    string Enqueue<T>(Expression<Action<T>> methodCall);
}
