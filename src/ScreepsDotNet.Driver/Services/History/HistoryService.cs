using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using ScreepsDotNet.Driver.Abstractions.Config;
using ScreepsDotNet.Driver.Abstractions.Eventing;
using ScreepsDotNet.Driver.Abstractions.History;
using ScreepsDotNet.Driver.Constants;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;
using StackExchange.Redis;

namespace ScreepsDotNet.Driver.Services.History;

internal sealed class HistoryService(
    IDriverConfig driverConfig,
    IRedisConnectionProvider redisProvider,
    IMongoDatabaseProvider databaseProvider,
    ILogger<HistoryService>? logger = null) : IHistoryService
{
    private static readonly JsonSerializerOptions HistorySerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IDatabase _redis = redisProvider.GetConnection().GetDatabase();
    private readonly IMongoCollection<RoomHistoryChunkDocument> _historyChunks =
        databaseProvider.GetCollection<RoomHistoryChunkDocument>(databaseProvider.Settings.RoomHistoryCollection);

    public Task SaveRoomHistoryAsync(string roomName, int gameTime, RoomHistoryTickPayload payload, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);
        ArgumentNullException.ThrowIfNull(payload);

        var serializedObjects = JsonSerializer.Serialize(payload, HistorySerializerOptions);
        var key = $"{RedisKeys.RoomHistory}{roomName}";
        var entry = new HashEntry(gameTime.ToString(), serializedObjects);
        return _redis.HashSetAsync(key, [entry]);
    }

    public async Task UploadRoomHistoryChunkAsync(string roomName, int baseGameTime, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);

        var key = $"{RedisKeys.RoomHistory}{roomName}";
        var entries = await _redis.HashGetAllAsync(key).ConfigureAwait(false);
        if (entries.Length == 0)
            return;

        var parsedTicks = new Dictionary<int, JsonNode?>(entries.Length);
        foreach (var entry in entries) {
            var (tick, payload) = ParseTick(entry);
            if (!tick.HasValue || string.IsNullOrWhiteSpace(payload))
                continue;

            parsedTicks[tick.Value] = ParseNode(payload);
        }

        var chunkTicks = HistoryDiffBuilder.BuildChunk(parsedTicks, baseGameTime);
        if (chunkTicks is null || chunkTicks.Count == 0)
            return;

        var chunk = new RoomHistoryChunk(
            roomName,
            baseGameTime,
            DateTimeOffset.UtcNow,
            chunkTicks);

        await PersistChunkAsync(chunk, token).ConfigureAwait(false);
        driverConfig.EmitRoomHistorySaved(new RoomHistorySavedEventArgs(roomName, baseGameTime, chunk));

        await _redis.KeyDeleteAsync(key).ConfigureAwait(false);
        logger?.LogDebug("Uploaded room history chunk for {Room} starting at {BaseTick}.", roomName, baseGameTime);
    }

    public IRoomStatsUpdater CreateRoomStatsUpdater(string roomName) => new RoomStatsUpdater(roomName, driverConfig);

    private static (int? Tick, string? Payload) ParseTick(HashEntry entry)
    {
        if (!entry.Name.HasValue || !entry.Value.HasValue)
            return (null, null);

        if (!int.TryParse(entry.Name.ToString(), out var tick))
            return (null, null);

        return (tick, entry.Value.ToString());
    }

    private static JsonNode? ParseNode(string payload)
    {
        try {
            return JsonNode.Parse(payload);
        }
        catch (JsonException) {
            return null;
        }
    }

    private Task PersistChunkAsync(RoomHistoryChunk chunk, CancellationToken token)
    {
        var document = new RoomHistoryChunkDocument
        {
            Id = $"{chunk.Room}:{chunk.BaseTick}",
            Room = chunk.Room,
            BaseTick = chunk.BaseTick,
            TimestampUtc = chunk.Timestamp.UtcDateTime,
            Ticks = SerializeTicks(chunk.Ticks)
        };

        return _historyChunks.ReplaceOneAsync(doc => doc.Id == document.Id, document, new ReplaceOptions { IsUpsert = true }, token);
    }

    private static Dictionary<string, string?> SerializeTicks(IReadOnlyDictionary<int, JsonNode?> ticks)
    {
        var serialized = new Dictionary<string, string?>(ticks.Count, StringComparer.Ordinal);
        foreach (var (tick, payload) in ticks)
            serialized[tick.ToString()] = SerializeNode(payload);
        return serialized;
    }

    private static string? SerializeNode(JsonNode? node) => node?.ToJsonString();

    private sealed class RoomStatsUpdater(string roomName, IDriverConfig config) : IRoomStatsUpdater
    {
        private readonly Dictionary<string, Dictionary<string, int>> _metrics = new(StringComparer.Ordinal);

        public void Increment(string userId, string metric, int amount)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(userId);
            ArgumentException.ThrowIfNullOrWhiteSpace(metric);

            if (!_metrics.TryGetValue(userId, out var userMetrics)) {
                userMetrics = new Dictionary<string, int>(StringComparer.Ordinal);
                _metrics[userId] = userMetrics;
            }

            userMetrics[metric] = userMetrics.TryGetValue(metric, out var existing)
                ? existing + amount
                : amount;
        }

        public Task FlushAsync(int gameTime, CancellationToken token = default)
        {
            if (_metrics.Count == 0)
                return Task.CompletedTask;

            var payload = new RoomStatsUpdate(roomName, gameTime, SnapshotMetrics(_metrics));
            config.EmitProcessorLoopStage(LoopStageNames.Processor.RoomStatsUpdated, payload);
            _metrics.Clear();
            return Task.CompletedTask;
        }

        private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> SnapshotMetrics(
            IReadOnlyDictionary<string, Dictionary<string, int>> metrics)
        {
            var clone = new Dictionary<string, IReadOnlyDictionary<string, int>>(metrics.Count, StringComparer.Ordinal);
            foreach (var (userId, values) in metrics) {
                if (string.IsNullOrWhiteSpace(userId))
                    continue;

                clone[userId] = new Dictionary<string, int>(values, StringComparer.Ordinal);
            }

            return clone;
        }
    }
}
