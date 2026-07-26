using MongoDB.Driver;

namespace IGBZ.Infrastructure.Mongo.Migrations;

/// <summary>
/// قرارداد مشترک برای مهاجرت‌های پایگاه داده مونو‌دی‌بی (فاز ۱ - نقشه راه).
/// </summary>
public interface IMongoMigration
{
    int Version { get; }
    string Description { get; }
    Task UpAsync(IMongoDatabase database, CancellationToken cancellationToken = default);
}
