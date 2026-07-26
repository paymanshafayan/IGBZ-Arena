using System.Linq.Expressions;
using IGBZ.Domain.Tenancy;

namespace IGBZ.Application.Abstractions;

/// <summary>
/// ریپازیتوری محدود به مستأجر (بخش ۴).
///
/// قرارداد امنیتی:
/// ۱. قید <c>where T : ITenantOwned</c> یعنی در سطح کامپایل نمی‌توان موجودیت بدون
///    <c>tenantId</c> را از این مسیر خواند.
/// ۲. هیچ متدی <c>tenantId</c> را به‌عنوان پارامتر نمی‌گیرد — از <see cref="ITenantContext"/>
///    تزریق‌شده در سازنده می‌آید، پس فراموش‌کردنی نیست.
/// ۳. پیاده‌سازی موظف است فیلتر <c>tenantId</c> را با AND به هر کوئری اضافه کند و
///    هنگام درج/به‌روزرسانی، تعلق سند به مستأجر جاری را تأیید کند.
/// </summary>
public interface ITenantScopedRepository<T>
    where T : class, ITenantOwned
{
    Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> ListAsync(
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);

    Task<long> CountAsync(
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default);

    Task InsertAsync(T entity, CancellationToken cancellationToken = default);

    Task<bool> ReplaceAsync(T entity, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
}

/// <summary>
/// وقتی موجودیتی متعلق به مستأجر دیگری از طریق ریپازیتوری مستأجر جاری دستکاری شود.
/// این استثنا باید همیشه به‌عنوان رخداد امنیتی لاگ شود.
/// </summary>
public sealed class CrossTenantAccessException(string message) : InvalidOperationException(message);
