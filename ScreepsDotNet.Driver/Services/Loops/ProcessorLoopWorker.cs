using System.Text.Json;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using ScreepsDotNet.Driver.Abstractions.Bulk;
using ScreepsDotNet.Driver.Abstractions.Config;
using ScreepsDotNet.Driver.Abstractions.Environment;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Notifications;
using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

namespace ScreepsDotNet.Driver.Services.Loops;

internal sealed class ProcessorLoopWorker(
    IRoomDataService roomDataService,
    IEnvironmentService environmentService,
    IDriverLoopHooks loopHooks,
    IDriverConfig config,
    IBulkWriterFactory bulkWriterFactory,
    ILogger<ProcessorLoopWorker>? logger = null) : IProcessorLoopWorker
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IRoomDataService _rooms = roomDataService;
    private readonly IEnvironmentService _environment = environmentService;
    private readonly IDriverLoopHooks _hooks = loopHooks;
    private readonly IDriverConfig _config = config;
    private readonly IBulkWriterFactory _bulkWriterFactory = bulkWriterFactory;
    private readonly ILogger<ProcessorLoopWorker>? _logger = logger;

    public async Task HandleRoomAsync(string roomName, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);

        var gameTime = await _environment.GetGameTimeAsync(token).ConfigureAwait(false);
        var roomObjects = await _rooms.GetRoomObjectsAsync(roomName, token).ConfigureAwait(false);
        var intents = await _rooms.GetRoomIntentsAsync(roomName, token).ConfigureAwait(false);

        if (intents is not null)
            await ApplyRoomIntentsAsync(roomName, roomObjects.Objects, intents, token).ConfigureAwait(false);

        await _rooms.ClearRoomIntentsAsync(roomName, token).ConfigureAwait(false);

        var historyPayload = JsonSerializer.Serialize(new
        {
            room = roomName,
            objects = roomObjects.Objects,
            users = roomObjects.Users
        }, JsonOptions);

        await _hooks.SaveRoomHistoryAsync(roomName, gameTime, historyPayload, token).ConfigureAwait(false);

        var chunkSize = Math.Max(_config.HistoryChunkSize, 1);
        if (gameTime % chunkSize == 0)
        {
            var chunkBase = Math.Max(gameTime - chunkSize + 1, 0);
            await _hooks.UploadRoomHistoryChunkAsync(roomName, chunkBase, token).ConfigureAwait(false);
        }
    }

    private async Task ApplyRoomIntentsAsync(string roomName, IReadOnlyDictionary<string, RoomObjectDocument> objects, RoomIntentDocument intents, CancellationToken token)
    {
        if (intents.Users is null || intents.Users.Count == 0)
            return;

        var writer = _bulkWriterFactory.CreateRoomObjectsWriter();
        var events = new List<RoomIntentEvent>();
        var notificationCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        foreach (var (userIdKey, intentPayload) in intents.Users)
        {
            if (intentPayload?.ObjectsManual is not { Count: > 0 })
                continue;

            var userId = (userIdKey ?? string.Empty).Trim();

            foreach (var (objectId, payload) in intentPayload.ObjectsManual)
            {
                if (string.IsNullOrWhiteSpace(objectId))
                    continue;

                var intentMetadata = new Dictionary<string, object?>
                {
                    ["lastIntentUser"] = userId,
                    ["lastIntentTime"] = timestamp
                };

                if (payload is not null && payload.ElementCount > 0)
                    intentMetadata["lastIntent"] = ConvertPayload(payload);

                writer.Update(objectId, intentMetadata);

                if (payload is not null)
                    ApplyMutations(writer, objectId, objects, payload);

                events.Add(new RoomIntentEvent(
                    userId,
                    objectId,
                    ConvertPayload(payload)));

                if (!string.IsNullOrWhiteSpace(userId))
                    notificationCounts[userId] = notificationCounts.TryGetValue(userId, out var existing) ? existing + 1 : 1;
            }
        }

        if (writer.HasPendingOperations)
            await writer.ExecuteAsync(token).ConfigureAwait(false);

        if (events.Count > 0)
        {
            var eventLogJson = JsonSerializer.Serialize(events, JsonOptions);
            await _rooms.SaveRoomEventLogAsync(roomName, eventLogJson, token).ConfigureAwait(false);

            var mapViewPayload = JsonSerializer.Serialize(new RoomIntentMapView(roomName, timestamp, events), JsonOptions);
            await _rooms.SaveMapViewAsync(roomName, mapViewPayload, token).ConfigureAwait(false);

            _logger?.LogDebug("Applied {Count} intents for room {Room}.", events.Count, roomName);
        }

        foreach (var (userId, count) in notificationCounts)
        {
            if (string.IsNullOrWhiteSpace(userId))
                continue;

            var message = $"Processed {count} intents in {roomName}.";
            await _hooks.SendNotificationAsync(userId, message, new NotificationOptions(5, "intent"), token).ConfigureAwait(false);
        }
    }

    private static void ApplyMutations(IBulkWriter<RoomObjectDocument> writer, string objectId, IReadOnlyDictionary<string, RoomObjectDocument> objects, BsonDocument payload)
    {
        objects.TryGetValue(objectId, out var target);
        var update = new Dictionary<string, object?>(StringComparer.Ordinal);

        if (payload.TryGetValue("remove", out var removeValue) && IsTruthy(removeValue))
        {
            writer.Remove(objectId);
            return;
        }

        if (payload.TryGetValue("damage", out var damageValue) && target is not null)
        {
            var damage = damageValue.ToInt32();
            if (damage > 0 && target.Hits.HasValue)
            {
                var newHits = Math.Max(0, target.Hits!.Value - damage);
                update["hits"] = newHits;
                target.Hits = newHits;
                if (newHits == 0)
                {
                    writer.Remove(objectId);
                    return;
                }
            }
        }

        if (payload.TryGetValue("set", out var setValue) && setValue is BsonDocument setDoc)
        {
            foreach (var element in setDoc.Elements)
                update[element.Name] = ConvertBsonValue(element.Value);
        }

        if (payload.TryGetValue("patch", out var patchValue) && patchValue is BsonDocument patchDoc) {
            foreach (var element in patchDoc.Elements)
                update[element.Name] = ConvertBsonValue(element.Value);
        }

        foreach (var element in payload.Elements)
        {
            if (element.Name is "damage" or "remove" or "set" or "patch")
                continue;
            update[element.Name] = ConvertBsonValue(element.Value);
        }

        if (update.Count > 0)
            writer.Update(objectId, update);
    }

    private static object? ConvertPayload(BsonDocument? payload)
    {
        if (payload is null || payload.ElementCount == 0)
            return null;

        return ConvertBsonValue(payload);
    }

    private static object? ConvertBsonValue(BsonValue value)
    {
        if (value.IsBsonNull)
            return null;

        return value.BsonType switch
        {
            BsonType.Boolean => value.AsBoolean,
            BsonType.Int32 => value.AsInt32,
            BsonType.Int64 => value.AsInt64,
            BsonType.Double => value.AsDouble,
            BsonType.Decimal128 => Decimal128.ToDouble(value.AsDecimal128),
            BsonType.String => value.AsString,
            BsonType.Document => value.AsBsonDocument.Elements.ToDictionary(
                element => element.Name,
                element => ConvertBsonValue(element.Value),
                StringComparer.Ordinal),
            BsonType.Array => value.AsBsonArray.Select(ConvertBsonValue).ToArray(),
            _ => value.ToString()
        };
    }

    private static bool IsTruthy(BsonValue value)
    {
        return value.BsonType switch
        {
            BsonType.Boolean => value.AsBoolean,
            BsonType.Int32 => value.AsInt32 != 0,
            BsonType.Int64 => value.AsInt64 != 0,
            BsonType.Double => Math.Abs(value.AsDouble) > double.Epsilon,
            BsonType.String => !string.IsNullOrWhiteSpace(value.AsString),
            _ => !value.IsBsonNull
        };
    }

    private sealed record RoomIntentEvent(string UserId, string ObjectId, object? Payload);

    private sealed record RoomIntentMapView(string Room, long Timestamp, IReadOnlyList<RoomIntentEvent> Intents);
}
