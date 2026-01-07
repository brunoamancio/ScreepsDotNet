using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Storage;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using StackExchange.Redis;

namespace ScreepsDotNet.Storage.MongoRedis.Adapters;

public sealed class MongoRedisStorageAdapter(IMongoDatabaseProvider databaseProvider, IRedisConnectionProvider redisConnectionProvider, ILogger<MongoRedisStorageAdapter> logger)
    : IStorageAdapter
{
    private const string MongoPingErrorMessage = "MongoDB ping failed";
    private const string RedisPingErrorMessage = "Redis ping failed";

    private readonly IMongoDatabase _database = databaseProvider.GetDatabase();
    private readonly IConnectionMultiplexer _redis = redisConnectionProvider.GetConnection();

    public async Task<StorageStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var details = new List<string>();
        var now = DateTimeOffset.UtcNow;

        var mongoHealthy = await PingMongoAsync(cancellationToken);
        if (!mongoHealthy)
            details.Add(MongoPingErrorMessage);

        var redisHealthy = await PingRedisAsync(cancellationToken);
        if (!redisHealthy)
            details.Add(RedisPingErrorMessage);

        var joinedDetails = details.Count == 0 ? null : string.Join(" | ", details);
        return new StorageStatus(mongoHealthy && redisHealthy, now, joinedDetails);
    }

    private async Task<bool> PingMongoAsync(CancellationToken cancellationToken)
    {
        try {
            await _database.RunCommandAsync((Command<BsonDocument>)"{ ping: 1 }", cancellationToken: cancellationToken);
            return true;
        }
        catch (Exception ex) {
            logger.LogError(ex, MongoPingErrorMessage);
            return false;
        }
    }

    private async Task<bool> PingRedisAsync(CancellationToken cancellationToken)
    {
        try {
            var db = _redis.GetDatabase();
            await db.PingAsync();
            return true;
        }
        catch (Exception ex) {
            logger.LogError(ex, RedisPingErrorMessage);
            return false;
        }
    }

}
