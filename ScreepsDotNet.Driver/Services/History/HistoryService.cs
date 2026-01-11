using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ScreepsDotNet.Driver.Abstractions.Config;
using ScreepsDotNet.Driver.Abstractions.Eventing;
using ScreepsDotNet.Driver.Abstractions.History;
using ScreepsDotNet.Driver.Constants;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using StackExchange.Redis;

namespace ScreepsDotNet.Driver.Services.History;

internal sealed class HistoryService(
    IDriverConfig driverConfig,
    IRedisConnectionProvider redisProvider,
    ILogger<HistoryService>? logger = null) : IHistoryService
{
    private readonly IDriverConfig _driverConfig = driverConfig;
    private readonly IDatabase _redis = redisProvider.GetConnection().GetDatabase();
    private readonly ILogger<HistoryService>? _logger = logger;

    public Task SaveRoomHistoryAsync(string roomName, int gameTime, string serializedObjects, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);
        ArgumentException.ThrowIfNullOrWhiteSpace(serializedObjects);

        var key = $"{RedisKeys.RoomHistory}{roomName}";
        var entry = new HashEntry(gameTime.ToString(), serializedObjects);
        return _redis.HashSetAsync(key, new[] { entry });
    }

    public async Task UploadRoomHistoryChunkAsync(string roomName, int baseGameTime, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);

        var key = $"{RedisKeys.RoomHistory}{roomName}";
        var entries = await _redis.HashGetAllAsync(key).ConfigureAwait(false);
        if (entries.Length == 0)
            return;

        var tickMap = entries
            .Select(ParseTick)
            .Where(tuple => tuple.Tick.HasValue && !string.IsNullOrWhiteSpace(tuple.Payload))
            .ToDictionary(tuple => tuple.Tick!.Value, tuple => tuple.Payload!, EqualityComparer<int>.Default);

        if (!tickMap.TryGetValue(baseGameTime, out var basePayload))
            return;

        var ticks = new SortedDictionary<int, JsonNode?>(Comparer<int>.Default);
        JsonNode? previous = null;
        var currentTick = baseGameTime;

        while (tickMap.TryGetValue(currentTick, out var payload))
        {
            var currentNode = ParseNode(payload);
            var valueToStore = currentTick == baseGameTime
                ? currentNode?.DeepClone()
                : CreateDiff(previous, currentNode);

            ticks[currentTick] = valueToStore;
            previous = currentNode;
            currentTick++;
        }

        var chunk = new RoomHistoryChunk(
            roomName,
            baseGameTime,
            DateTimeOffset.UtcNow,
            new Dictionary<int, JsonNode?>(ticks));

        _driverConfig.EmitRoomHistorySaved(new RoomHistorySavedEventArgs(roomName, baseGameTime, chunk));

        await _redis.KeyDeleteAsync(key).ConfigureAwait(false);
        _logger?.LogDebug("Uploaded room history chunk for {Room} starting at {BaseTick}.", roomName, baseGameTime);
    }

    public IRoomStatsUpdater CreateRoomStatsUpdater(string roomName) => new RoomStatsUpdater(roomName, _driverConfig);

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
        try
        {
            return JsonNode.Parse(payload);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonNode? CreateDiff(JsonNode? previous, JsonNode? current)
    {
        if (previous is null || current is null)
            return current?.DeepClone();

        if (previous.GetType() != current.GetType())
            return current.DeepClone();

        if (current is JsonValue)
            return current.DeepClone();

        if (current is JsonArray)
            return current.DeepClone();

        if (current is JsonObject currentObject && previous is JsonObject previousObject)
        {
            var result = new JsonObject();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in currentObject)
                keys.Add(property.Key);
            foreach (var property in previousObject)
                keys.Add(property.Key);

            foreach (var key in keys)
            {
                var previousChild = previousObject[key];
                var currentChild = currentObject[key];

                if (currentChild is null)
                {
                    result[key] = null;
                    continue;
                }

                if (previousChild is null)
                {
                    result[key] = currentChild.DeepClone();
                    continue;
                }

                if (JsonNode.DeepEquals(previousChild, currentChild))
                    continue;

                result[key] = CreateDiff(previousChild, currentChild);
            }

            return result;
        }

        return current.DeepClone();
    }

    private sealed class RoomStatsUpdater(string roomName, IDriverConfig config) : IRoomStatsUpdater
    {
        private readonly string _roomName = roomName;
        private readonly IDriverConfig _config = config;
        private readonly Dictionary<string, Dictionary<string, int>> _metrics = new(StringComparer.Ordinal);

        public void Increment(string userId, string metric, int amount)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(userId);
            ArgumentException.ThrowIfNullOrWhiteSpace(metric);

            if (!_metrics.TryGetValue(userId, out var userMetrics))
            {
                userMetrics = new Dictionary<string, int>(StringComparer.Ordinal);
                _metrics[userId] = userMetrics;
            }

            userMetrics[metric] = userMetrics.TryGetValue(metric, out var existing)
                ? existing + amount
                : amount;
        }

        public Task FlushAsync(CancellationToken token = default)
        {
            if (_metrics.Count == 0)
                return Task.CompletedTask;

            _config.EmitProcessorLoopStage("roomStatsUpdated", new { Room = _roomName, Metrics = _metrics });
            _metrics.Clear();
            return Task.CompletedTask;
        }
    }
}
