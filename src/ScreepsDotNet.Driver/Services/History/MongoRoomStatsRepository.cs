namespace ScreepsDotNet.Driver.Services.History;

using MongoDB.Driver;
using ScreepsDotNet.Driver.Abstractions.History;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

internal sealed class MongoRoomStatsRepository : IRoomStatsRepository
{
    private readonly IMongoCollection<RoomStatsDocument> _collection;

    public MongoRoomStatsRepository(IMongoDatabaseProvider databaseProvider)
    {
        ArgumentNullException.ThrowIfNull(databaseProvider);
        _collection = databaseProvider.GetCollection<RoomStatsDocument>(databaseProvider.Settings.RoomStatsCollection);
    }

    public Task AppendAsync(RoomStatsUpdate update, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        if (string.IsNullOrWhiteSpace(update.Room))
            throw new ArgumentException("Room identifier is required.", nameof(update));

        var document = new RoomStatsDocument
        {
            Id = $"{update.Room}:{update.GameTime}",
            Room = update.Room,
            Tick = update.GameTime,
            TimestampUtc = DateTime.UtcNow,
            Metrics = CloneMetrics(update.Metrics)
        };

        return _collection.ReplaceOneAsync(
            existing => existing.Id == document.Id,
            document,
            new ReplaceOptions { IsUpsert = true },
            token);
    }

    private static Dictionary<string, Dictionary<string, int>> CloneMetrics(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> metrics)
    {
        var clone = new Dictionary<string, Dictionary<string, int>>(metrics.Count, StringComparer.Ordinal);
        foreach (var (userId, userMetrics) in metrics) {
            if (string.IsNullOrWhiteSpace(userId))
                continue;

            clone[userId] = new Dictionary<string, int>(userMetrics, StringComparer.Ordinal);
        }

        return clone;
    }
}
