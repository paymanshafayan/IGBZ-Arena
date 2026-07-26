using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace IGBZ.Infrastructure.Mongo.Migrations;

public sealed class AppliedMigration
{
    [BsonId]
    public int Version { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime AppliedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// مجری خودکار مهاجرت‌های مونو‌دی‌بی (فاز ۱ - نقشه راه).
/// </summary>
public sealed class MongoMigrationRunner(IMongoDatabase database)
{
    private readonly IMongoCollection<AppliedMigration> _migrationsCollection =
        database.GetCollection<AppliedMigration>("platform_migrations");

    public async Task RunAsync(
        IEnumerable<IMongoMigration> migrations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(migrations);

        var applied = await _migrationsCollection.Find(FilterDefinition<AppliedMigration>.Empty)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var appliedVersions = applied.Select(m => m.Version).ToHashSet();

        var pending = migrations
            .Where(m => !appliedVersions.Contains(m.Version))
            .OrderBy(m => m.Version)
            .ToList();

        foreach (var migration in pending)
        {
            // اعمال تراکنشی مهاجرت
            await migration.UpAsync(database, cancellationToken).ConfigureAwait(false);

            var record = new AppliedMigration
            {
                Version = migration.Version,
                Description = migration.Description
            };

            await _migrationsCollection.InsertOneAsync(record, options: null, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
