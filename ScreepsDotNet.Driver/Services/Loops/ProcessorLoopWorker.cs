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
                {
                    objects.TryGetValue(objectId, out var actor);
                    ApplyTypedIntents(writer, objectId, actor, objects, payload);
                    ApplyMutations(writer, objectId, objects, payload);
                }

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

    private static void ApplyTypedIntents(IBulkWriter<RoomObjectDocument> writer, string actorId, RoomObjectDocument? actor, IReadOnlyDictionary<string, RoomObjectDocument> objects, BsonDocument payload)
    {
        if (actor?.Type is null || payload.ElementCount == 0)
            return;

        if (actor.Type == "creep" || actor.Type == "powerCreep" || actor.Type == "tower")
            ApplyCombatIntents(writer, objects, payload);
        else if (actor.Type == "link")
            ApplyLinkIntents(writer, actorId, actor, objects, payload);
        else if (actor.Type == "lab") ApplyLabIntents(writer, actorId, actor, payload);
    }

    private static void ApplyCombatIntents(IBulkWriter<RoomObjectDocument> writer, IReadOnlyDictionary<string, RoomObjectDocument> objects, BsonDocument payload)
    {
        if (payload.TryGetValue("attack", out var attack) && attack is BsonDocument attackDoc)
            ApplyDamageIntent(writer, objects, attackDoc);

        if (payload.TryGetValue("rangedAttack", out var rangedAttack) && rangedAttack is BsonDocument rangedDoc)
            ApplyDamageIntent(writer, objects, rangedDoc);

        if (payload.TryGetValue("heal", out var heal) && heal is BsonDocument healDoc)
            ApplyHealIntent(writer, objects, healDoc);

        if (payload.TryGetValue("rangedHeal", out var rangedHeal) && rangedHeal is BsonDocument rangedHealDoc)
            ApplyHealIntent(writer, objects, rangedHealDoc);
    }

    private static void ApplyLinkIntents(IBulkWriter<RoomObjectDocument> writer, string actorId, RoomObjectDocument actor, IReadOnlyDictionary<string, RoomObjectDocument> objects, BsonDocument payload)
    {
        if (!payload.TryGetValue("transferEnergy", out var transfer) || transfer is not BsonDocument transferDoc)
            return;

        var targetId = transferDoc.TryGetValue("id", out var idValue) ? idValue.AsString : null;
        var amount = transferDoc.TryGetValue("amount", out var amountValue) ? amountValue.ToInt32() : 0;
        if (string.IsNullOrWhiteSpace(targetId) || amount <= 0)
            return;

        objects.TryGetValue(targetId, out var target);
        if (target is null)
            return;

        var sourceEnergy = Math.Max(0, GetStoreValue(actor, "energy") - amount);
        writer.Update(actorId, new { store = new Dictionary<string, int> { ["energy"] = sourceEnergy } });

        var targetEnergy = GetStoreValue(target, "energy") + amount;
        writer.Update(targetId, new { store = new Dictionary<string, int> { ["energy"] = targetEnergy } });
    }

    private static void ApplyLabIntents(IBulkWriter<RoomObjectDocument> writer, string actorId, RoomObjectDocument actor, BsonDocument payload)
    {
        if (!payload.TryGetValue("runReaction", out var reaction) || reaction is not BsonDocument reactionDoc)
            return;

        if (!reactionDoc.TryGetValue("resourceType", out var resourceValue))
            return;

        var resourceType = resourceValue.AsString;
        if (string.IsNullOrWhiteSpace(resourceType))
            return;

        var store = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["energy"] = Math.Max(0, GetStoreValue(actor, "energy") - 2),
            [resourceType] = GetStoreValue(actor, resourceType) + 1
        };

        writer.Update(actorId, new { store });
    }

    private static void ApplyDamageIntent(IBulkWriter<RoomObjectDocument> writer, IReadOnlyDictionary<string, RoomObjectDocument> objects, BsonDocument intent)
    {
        var targetId = intent.TryGetValue("id", out var idValue) ? idValue.AsString : null;
        var damage = intent.TryGetValue("damage", out var damageValue) ? damageValue.ToInt32() : 0;
        if (string.IsNullOrWhiteSpace(targetId) || damage <= 0)
            return;

        objects.TryGetValue(targetId, out var target);
        if (target?.Hits is null)
            return;

        var hits = Math.Max(0, target.Hits.Value - damage);
        target.Hits = hits;
        if (hits == 0)
            writer.Remove(targetId);
        else
            writer.Update(targetId, new { hits });
    }

    private static void ApplyHealIntent(IBulkWriter<RoomObjectDocument> writer, IReadOnlyDictionary<string, RoomObjectDocument> objects, BsonDocument intent)
    {
        var targetId = intent.TryGetValue("id", out var idValue) ? idValue.AsString : null;
        var amount = intent.TryGetValue("amount", out var amountValue) ? amountValue.ToInt32() : 0;
        if (string.IsNullOrWhiteSpace(targetId) || amount <= 0)
            return;

        objects.TryGetValue(targetId, out var target);
        if (target?.Hits is null)
            return;

        var maxHits = target.HitsMax ?? int.MaxValue;
        var hits = Math.Min(maxHits, target.Hits.Value + amount);
        target.Hits = hits;
        writer.Update(targetId, new { hits });
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

    private static int GetStoreValue(RoomObjectDocument document, string resource)
        => document.Store?.GetValueOrDefault(resource, 0) ?? 0;

    private sealed record RoomIntentEvent(string UserId, string ObjectId, object? Payload);

    private sealed record RoomIntentMapView(string Room, long Timestamp, IReadOnlyList<RoomIntentEvent> Intents);
}
