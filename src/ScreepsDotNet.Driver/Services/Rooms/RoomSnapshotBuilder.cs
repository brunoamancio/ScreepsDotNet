using MongoDB.Bson;
using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

namespace ScreepsDotNet.Driver.Services.Rooms;

internal sealed class RoomSnapshotBuilder(IRoomDataService roomDataService) : IRoomSnapshotBuilder
{

    public async Task<RoomSnapshot> BuildAsync(string roomName, int gameTime, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);

        var objectsTask = roomDataService.GetRoomObjectsAsync(roomName, token);
        var flagsTask = roomDataService.GetRoomFlagsAsync(roomName, token);
        var terrainTask = roomDataService.GetRoomTerrainAsync(roomName, token);
        var infoTask = roomDataService.GetRoomInfoAsync(roomName, token);
        var intentsTask = roomDataService.GetRoomIntentsAsync(roomName, token);

        await Task.WhenAll(objectsTask, flagsTask, terrainTask, infoTask, intentsTask).ConfigureAwait(false);

        var objectsPayload = await objectsTask.ConfigureAwait(false);
        var flags = await flagsTask.ConfigureAwait(false);
        var terrain = await terrainTask.ConfigureAwait(false);
        var roomInfo = await infoTask.ConfigureAwait(false);
        var intents = await intentsTask.ConfigureAwait(false);

        return new RoomSnapshot(
            roomName,
            gameTime,
            RoomContractMapper.MapRoomInfo(roomInfo),
            RoomContractMapper.MapRoomObjects(objectsPayload.Objects),
            RoomContractMapper.MapUsers(objectsPayload.Users),
            MapRoomIntents(intents),
            MapTerrain(terrain),
            MapFlags(flags));
    }

    private static IReadOnlyDictionary<string, RoomTerrainSnapshot> MapTerrain(IReadOnlyDictionary<string, RoomTerrainDocument> documents)
    {
        var result = new Dictionary<string, RoomTerrainSnapshot>(documents.Count, StringComparer.Ordinal);
        foreach (var (id, document) in documents) {
            if (document is null) continue;
            result[id] = new RoomTerrainSnapshot(
                id,
                document.Room,
                document.Shard,
                document.Type,
                document.Terrain);
        }

        return result;
    }

    private static IReadOnlyList<RoomFlagSnapshot> MapFlags(IReadOnlyList<RoomFlagDocument> documents)
    {
        if (documents.Count == 0) return [];
        var result = new RoomFlagSnapshot[documents.Count];
        for (var i = 0; i < documents.Count; i++) {
            var doc = documents[i];
            result[i] = new RoomFlagSnapshot(doc.Id ?? string.Empty, doc.UserId, doc.Room ?? string.Empty, doc.Shard, doc.Data);
        }

        return result;
    }

    private static RoomIntentSnapshot? MapRoomIntents(RoomIntentDocument? document)
    {
        if (document is null) return null;

        var users = document.Users is null
            ? new Dictionary<string, IntentEnvelope>(StringComparer.Ordinal)
            : document.Users.ToDictionary(
                kvp => kvp.Key,
                kvp => MapIntentEnvelope(kvp.Key, kvp.Value),
                StringComparer.Ordinal);

        return new RoomIntentSnapshot(
            document.Room ?? string.Empty,
            document.Shard,
            users);
    }

    private static IntentEnvelope MapIntentEnvelope(string userId, RoomIntentUserDocument? document)
    {
        var objectIntents = MapObjectIntents(document?.ObjectsManual);

        var spawnIntents = MapSpawnIntents(document?.ObjectsManual);
        var creepIntents = MapCreepIntents(document?.ObjectsManual);
        var terminalIntents = new Dictionary<string, TerminalIntentEnvelope>(StringComparer.Ordinal);

        return new IntentEnvelope(userId, objectIntents, spawnIntents, creepIntents, terminalIntents);
    }

    private static IReadOnlyDictionary<string, SpawnIntentEnvelope> MapSpawnIntents(Dictionary<string, BsonDocument>? objectsManual)
    {
        var result = new Dictionary<string, SpawnIntentEnvelope>(StringComparer.Ordinal);
        if (objectsManual is null)
            return result;

        foreach (var (objectId, payload) in objectsManual) {
            if (string.IsNullOrWhiteSpace(objectId))
                continue;

            if (!SpawnIntentMapper.TryMap(payload, out var envelope))
                continue;

            result[objectId] = envelope;
        }

        return result;
    }

    private static IReadOnlyDictionary<string, CreepIntentEnvelope> MapCreepIntents(Dictionary<string, BsonDocument>? objectsManual)
    {
        var result = new Dictionary<string, CreepIntentEnvelope>(StringComparer.Ordinal);
        if (objectsManual is null)
            return result;

        foreach (var (objectId, payload) in objectsManual) {
            if (string.IsNullOrWhiteSpace(objectId))
                continue;

            if (!CreepIntentMapper.TryMap(payload, out var envelope))
                continue;

            result[objectId] = envelope;
        }

        return result;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<IntentRecord>> MapObjectIntents(Dictionary<string, BsonDocument>? objectsManual)
    {
        if (objectsManual is null || objectsManual.Count == 0)
            return new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal);

        var result = new Dictionary<string, IReadOnlyList<IntentRecord>>(objectsManual.Count, StringComparer.Ordinal);
        foreach (var (objectId, payload) in objectsManual) {
            if (string.IsNullOrWhiteSpace(objectId))
                continue;

            result[objectId] = IntentDocumentMapper.MapIntentRecords(payload);
        }

        return result;
    }

}
