using System.Text.Json;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using ScreepsDotNet.Common;
using ScreepsDotNet.Driver.Abstractions;
using ScreepsDotNet.Driver.Abstractions.Config;
using ScreepsDotNet.Driver.Abstractions.Environment;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Notifications;
using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Driver.Constants;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

namespace ScreepsDotNet.Driver.Services.Loops;

internal sealed class ProcessorLoopWorker(
    IRoomDataService roomDataService,
    IRoomSnapshotProvider snapshotProvider,
    IRoomMutationDispatcher mutationDispatcher,
    IEnvironmentService environmentService,
    IDriverLoopHooks loopHooks,
    IDriverConfig config,
    ILogger<ProcessorLoopWorker>? logger = null) : IProcessorLoopWorker
{
    private readonly ILogger<ProcessorLoopWorker>? _logger = logger;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task HandleRoomAsync(string roomName, int? queueDepth, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);

        var gameTime = await environmentService.GetGameTimeAsync(token).ConfigureAwait(false);
        var snapshot = await snapshotProvider.GetSnapshotAsync(roomName, gameTime, token).ConfigureAwait(false);
        var roomObjects = RehydrateObjects(snapshot.Objects);
        var users = RehydrateUsers(snapshot.Users);

        if (snapshot.Intents is not null)
            await ApplyRoomIntentsAsync(snapshot, roomObjects, token).ConfigureAwait(false);

        await roomDataService.ClearRoomIntentsAsync(roomName, token).ConfigureAwait(false);
        snapshotProvider.Invalidate(roomName);

        var historyPayload = JsonSerializer.Serialize(new
        {
            room = roomName,
            objects = roomObjects,
            users
        }, JsonOptions);

        await loopHooks.SaveRoomHistoryAsync(roomName, gameTime, historyPayload, token).ConfigureAwait(false);

        var chunkSize = Math.Max(config.HistoryChunkSize, 1);
        if (gameTime % chunkSize == 0)
        {
            var chunkBase = Math.Max(gameTime - chunkSize + 1, 0);
            await loopHooks.UploadRoomHistoryChunkAsync(roomName, chunkBase, token).ConfigureAwait(false);
        }

        var telemetry = new RuntimeTelemetryPayload(
            Loop: DriverProcessType.Processor,
            UserId: roomName,
            GameTime: gameTime,
            CpuLimit: 0,
            CpuBucket: 0,
            CpuUsed: 0,
            TimedOut: false,
            ScriptError: false,
            HeapUsedBytes: 0,
            HeapSizeLimitBytes: 0,
            ErrorMessage: null,
            QueueDepth: queueDepth,
            ColdStartRequested: false,
            Stage: LoopStageNames.Processor.TelemetryProcessRoom);
        await loopHooks.PublishRuntimeTelemetryAsync(telemetry, token).ConfigureAwait(false);
    }

    private async Task ApplyRoomIntentsAsync(RoomSnapshot snapshot, IReadOnlyDictionary<string, RoomObjectDocument> objects, CancellationToken token)
    {
        if (snapshot.Intents?.Users is null || snapshot.Intents.Users.Count == 0)
            return;

        var patches = new Dictionary<string, BsonDocument>(StringComparer.Ordinal);
        var removals = new List<string>();
        var events = new List<RoomIntentEvent>();
        var notificationCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        foreach (var (userIdKey, intentPayload) in snapshot.Intents.Users)
        {
            if (intentPayload?.ObjectsManualJson is not { Count: > 0 })
                continue;

            var userId = (userIdKey ?? string.Empty).Trim();

            foreach (var (objectId, payloadJson) in intentPayload.ObjectsManualJson)
            {
                if (string.IsNullOrWhiteSpace(objectId))
                    continue;

                var payload = TryParseDocument(payloadJson);

                var metadata = new BsonDocument
                {
                    [IntentMetadataKeys.LastIntentUser] = userId,
                    [IntentMetadataKeys.LastIntentTime] = timestamp
                };

                if (payload is not null && payload.ElementCount > 0)
                    metadata[IntentMetadataKeys.LastIntent] = payload.DeepClone();

                StagePatch(patches, objectId, metadata);

                if (payload is not null)
                {
                    objects.TryGetValue(objectId, out var actor);
                    ApplyTypedIntents(patches, removals, objectId, actor, objects, payload);
                    ApplyMutations(patches, removals, objectId, objects, payload);
                }

                events.Add(new RoomIntentEvent(
                    userId,
                    objectId,
                    ConvertPayload(payload)));

                if (!string.IsNullOrWhiteSpace(userId))
                    notificationCounts[userId] = notificationCounts.TryGetValue(userId, out var existing) ? existing + 1 : 1;
            }
        }

        var eventLogJson = events.Count > 0 ? JsonSerializer.Serialize(events, JsonOptions) : null;
        var mapViewPayload = events.Count > 0 ? JsonSerializer.Serialize(new RoomIntentMapView(snapshot.RoomName, timestamp, events), JsonOptions) : null;

        if (events.Count > 0)
            _logger?.LogDebug("Applied {Count} intents for room {Room}.", events.Count, snapshot.RoomName);

        foreach (var (userId, count) in notificationCounts)
        {
            if (string.IsNullOrWhiteSpace(userId))
                continue;

            var message = $"Processed {count} intents in {snapshot.RoomName}.";
            await loopHooks.SendNotificationAsync(userId, message, new NotificationOptions(5, "intent"), token).ConfigureAwait(false);
        }

        var patchList = patches.Select(pair => new RoomObjectPatch(pair.Key, pair.Value.ToJson())).ToArray();
        if (patchList.Length == 0 && removals.Count == 0 && string.IsNullOrWhiteSpace(eventLogJson) && string.IsNullOrWhiteSpace(mapViewPayload))
            return;

        var batch = new RoomMutationBatch(
            snapshot.RoomName,
            Array.Empty<RoomObjectUpsert>(),
            patchList,
            removals,
            null,
            mapViewPayload,
            eventLogJson);

        await mutationDispatcher.ApplyAsync(batch, token).ConfigureAwait(false);
    }

    private static void ApplyTypedIntents(
        IDictionary<string, BsonDocument> patches,
        ICollection<string> removals,
        string actorId,
        RoomObjectDocument? actor,
        IReadOnlyDictionary<string, RoomObjectDocument> objects,
        BsonDocument payload)
    {
        if (actor?.Type is null || payload.ElementCount == 0)
            return;

        if (actor.Type is RoomObjectTypes.Creep or RoomObjectTypes.PowerCreep or RoomObjectTypes.Tower)
            ApplyCombatIntents(patches, removals, objects, payload);
        else if (actor.Type == RoomObjectTypes.Link)
            ApplyLinkIntents(patches, actorId, actor, objects, payload);
        else if (actor.Type == RoomObjectTypes.Lab)
            ApplyLabIntents(patches, actorId, actor, payload);
    }

    private static void ApplyCombatIntents(
        IDictionary<string, BsonDocument> patches,
        ICollection<string> removals,
        IReadOnlyDictionary<string, RoomObjectDocument> objects,
        BsonDocument payload)
    {
        if (payload.TryGetValue(IntentActionType.Attack.ToKey(), out var attack) && attack is BsonDocument attackDoc)
            ApplyDamageIntent(patches, removals, objects, attackDoc);

        if (payload.TryGetValue(IntentActionType.RangedAttack.ToKey(), out var rangedAttack) && rangedAttack is BsonDocument rangedDoc)
            ApplyDamageIntent(patches, removals, objects, rangedDoc);

        if (payload.TryGetValue(IntentActionType.Heal.ToKey(), out var heal) && heal is BsonDocument healDoc)
            ApplyHealIntent(patches, objects, healDoc);

        if (payload.TryGetValue(IntentActionType.RangedHeal.ToKey(), out var rangedHeal) && rangedHeal is BsonDocument rangedHealDoc)
            ApplyHealIntent(patches, objects, rangedHealDoc);
    }

    private static void ApplyLinkIntents(
        IDictionary<string, BsonDocument> patches,
        string actorId,
        RoomObjectDocument actor,
        IReadOnlyDictionary<string, RoomObjectDocument> objects,
        BsonDocument payload)
    {
        if (!payload.TryGetValue(IntentActionType.TransferEnergy.ToKey(), out var transfer) || transfer is not BsonDocument transferDoc)
            return;

        var targetId = transferDoc.TryGetValue(IntentKeys.TargetId, out var idValue) ? idValue.AsString : null;
        var amount = transferDoc.TryGetValue(IntentKeys.Amount, out var amountValue) ? amountValue.ToInt32() : 0;
        if (string.IsNullOrWhiteSpace(targetId) || amount <= 0)
            return;

        objects.TryGetValue(targetId, out var target);
        if (target is null)
            return;

        var sourceEnergy = Math.Max(0, GetStoreValue(actor, "energy") - amount);
        StagePatch(patches, actorId, new BsonDocument("store", new BsonDocument("energy", sourceEnergy)));

        var targetEnergy = GetStoreValue(target, "energy") + amount;
        StagePatch(patches, targetId, new BsonDocument("store", new BsonDocument("energy", targetEnergy)));
    }

    private static void ApplyLabIntents(
        IDictionary<string, BsonDocument> patches,
        string actorId,
        RoomObjectDocument actor,
        BsonDocument payload)
    {
        if (!payload.TryGetValue(IntentActionType.RunReaction.ToKey(), out var reaction) || reaction is not BsonDocument reactionDoc)
            return;

        if (!reactionDoc.TryGetValue(IntentKeys.ResourceType, out var resourceValue))
            return;

        var resourceType = resourceValue.AsString;
        if (string.IsNullOrWhiteSpace(resourceType))
            return;

        var store = new BsonDocument
        {
            ["energy"] = Math.Max(0, GetStoreValue(actor, "energy") - 2),
            [resourceType] = GetStoreValue(actor, resourceType) + 1
        };

        StagePatch(patches, actorId, new BsonDocument("store", store));
    }

    private static void ApplyDamageIntent(
        IDictionary<string, BsonDocument> patches,
        ICollection<string> removals,
        IReadOnlyDictionary<string, RoomObjectDocument> objects,
        BsonDocument intent)
    {
        var targetId = intent.TryGetValue(IntentKeys.TargetId, out var idValue) ? idValue.AsString : null;
        var damage = intent.TryGetValue(IntentKeys.Damage, out var damageValue) ? damageValue.ToInt32() : 0;
        if (string.IsNullOrWhiteSpace(targetId) || damage <= 0)
            return;

        objects.TryGetValue(targetId, out var target);
        if (target?.Hits is null)
            return;

        var hits = Math.Max(0, target.Hits.Value - damage);
        target.Hits = hits;
        if (hits == 0)
            removals.Add(targetId);
        else
            StagePatch(patches, targetId, new BsonDocument("hits", hits));
    }

    private static void ApplyHealIntent(
        IDictionary<string, BsonDocument> patches,
        IReadOnlyDictionary<string, RoomObjectDocument> objects,
        BsonDocument intent)
    {
        var targetId = intent.TryGetValue(IntentKeys.TargetId, out var idValue) ? idValue.AsString : null;
        var amount = intent.TryGetValue(IntentKeys.Amount, out var amountValue) ? amountValue.ToInt32() : 0;
        if (string.IsNullOrWhiteSpace(targetId) || amount <= 0)
            return;

        objects.TryGetValue(targetId, out var target);
        if (target?.Hits is null)
            return;

        var maxHits = target.HitsMax ?? int.MaxValue;
        var hits = Math.Min(maxHits, target.Hits.Value + amount);
        target.Hits = hits;
        StagePatch(patches, targetId, new BsonDocument("hits", hits));
    }

    private static void ApplyMutations(
        IDictionary<string, BsonDocument> patches,
        ICollection<string> removals,
        string objectId,
        IReadOnlyDictionary<string, RoomObjectDocument> objects,
        BsonDocument payload)
    {
        objects.TryGetValue(objectId, out var target);
        var updateDocument = new BsonDocument();

        if (payload.TryGetValue(IntentKeys.Remove, out var removeValue) && IsTruthy(removeValue))
        {
            removals.Add(objectId);
            return;
        }

        if (payload.TryGetValue(IntentKeys.Damage, out var damageValue) && target is not null)
        {
            var damage = damageValue.ToInt32();
            if (damage > 0 && target.Hits.HasValue)
            {
                var newHits = Math.Max(0, target.Hits!.Value - damage);
                updateDocument["hits"] = newHits;
                target.Hits = newHits;
                if (newHits == 0)
                {
                    removals.Add(objectId);
                    return;
                }
            }
        }

        if (payload.TryGetValue(IntentKeys.Set, out var setValue) && setValue is BsonDocument setDoc)
            MergeDocuments(updateDocument, setDoc);

        if (payload.TryGetValue(IntentKeys.Patch, out var patchValue) && patchValue is BsonDocument patchDoc) MergeDocuments(updateDocument, patchDoc);

        foreach (var element in payload.Elements)
        {
            if (element.Name is IntentKeys.Damage or IntentKeys.Remove or IntentKeys.Set or IntentKeys.Patch)
                continue;
            updateDocument[element.Name] = element.Value.DeepClone();
        }

        if (updateDocument.ElementCount > 0)
            StagePatch(patches, objectId, updateDocument);
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

    private static IReadOnlyDictionary<string, RoomObjectDocument> RehydrateObjects(IReadOnlyDictionary<string, RoomObjectState> states)
    {
        var result = new Dictionary<string, RoomObjectDocument>(states.Count, StringComparer.Ordinal);
        foreach (var (id, state) in states)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(state.RawJson))
                continue;

            result[id] = BsonSerializer.Deserialize<RoomObjectDocument>(state.RawJson);
        }

        return result;
    }

    private static IReadOnlyDictionary<string, UserDocument> RehydrateUsers(IReadOnlyDictionary<string, UserState> states)
    {
        var result = new Dictionary<string, UserDocument>(states.Count, StringComparer.Ordinal);
        foreach (var (id, state) in states)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(state.RawJson))
                continue;

            result[id] = BsonSerializer.Deserialize<UserDocument>(state.RawJson);
        }
        return result;
    }

    private static BsonDocument? TryParseDocument(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        return BsonDocument.Parse(json);
    }

    private static void StagePatch(IDictionary<string, BsonDocument> patches, string objectId, BsonDocument delta)
    {
        if (string.IsNullOrWhiteSpace(objectId) || delta.ElementCount == 0)
            return;

        if (patches.TryGetValue(objectId, out var existing))
        {
            MergeDocuments(existing, delta);
            return;
        }

        patches[objectId] = delta.DeepClone().AsBsonDocument;
    }

    private static void MergeDocuments(BsonDocument target, BsonDocument source)
    {
        foreach (var element in source)
        {
            if (element.Value is BsonDocument child &&
                target.TryGetValue(element.Name, out var existing) &&
                existing is BsonDocument existingDocument)
            {
                MergeDocuments(existingDocument, child);
                continue;
            }

            target[element.Name] = element.Value.DeepClone();
        }
    }

    private static class IntentMetadataKeys
    {
        public const string LastIntentUser = "lastIntentUser";
        public const string LastIntentTime = "lastIntentTime";
        public const string LastIntent = "lastIntent";
    }

    private sealed record RoomIntentEvent(string UserId, string ObjectId, object? Payload);

    private sealed record RoomIntentMapView(string Room, long Timestamp, IReadOnlyList<RoomIntentEvent> Intents);
}
