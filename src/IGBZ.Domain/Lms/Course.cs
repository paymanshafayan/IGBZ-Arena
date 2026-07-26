using IGBZ.Domain.Tenancy;

namespace IGBZ.Domain.Lms;

/// <summary>
/// موجودیت درس‌های یک دوره (فاز ۱۰ - سیستم آموزش).
/// </summary>
public sealed class CourseLesson
{
    public CourseLesson(string id, string title, string videoHlsUrl, int durationMinutes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(videoHlsUrl);

        if (durationMinutes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationMinutes));
        }

        Id = id;
        Title = title;
        VideoHlsUrl = videoHlsUrl;
        DurationMinutes = durationMinutes;
    }

    public string Id { get; }
    public string Title { get; }
    public string VideoHlsUrl { get; }
    public int DurationMinutes { get; }
}

/// <summary>
/// موجودیت دوره آموزشی به عنوان یکی از انواع پیشرفته محصول (فاز ۱۰).
/// </summary>
public sealed class Course : TenantEntity
{
    private readonly List<CourseLesson> _lessons = [];

    public Course(string id, TenantId tenantId, string productId, string title)
        : base(id, tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        ProductId = productId;
        Title = title;
    }

    /// <summary>سازنده بازسازی برای لایه ذخیره‌سازی.</summary>
    public Course(
        string id,
        string tenantId,
        string productId,
        string title,
        IEnumerable<CourseLesson> lessons,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? updatedAtUtc)
        : base(id, new TenantId(tenantId))
    {
        ProductId = productId;
        Title = title;
        _lessons = [.. lessons];
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public string ProductId { get; private set; }

    public string Title { get; private set; }

    public IReadOnlyList<CourseLesson> Lessons => _lessons;

    public void AddLesson(CourseLesson lesson)
    {
        ArgumentNullException.ThrowIfNull(lesson);
        _lessons.Add(lesson);
        Touch();
    }
}
