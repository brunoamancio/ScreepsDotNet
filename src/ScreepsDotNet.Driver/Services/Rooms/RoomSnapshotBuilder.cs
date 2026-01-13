using MongoDB.Bson;
using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Driver.Extensions;
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
            MapFlags(flags),
            roomInfo.ToStableJson());
    }

    private static IReadOnlyDictionary<string, RoomTerrainSnapshot> MapTerrain(IReadOnlyDictionary<string, RoomTerrainDocument> documents)
    {
        var result = new Dictionary<string, RoomTerrainSnapshot>(documents.Count, StringComparer.Ordinal);
        foreach (var (id, document) in documents)
        {
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
        for (var i = 0; i < documents.Count; i++)
        {
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
            users,
            document.ToStableJson());
    }

    private static IntentEnvelope MapIntentEnvelope(string userId, RoomIntentUserDocument? document)
    {
        var manual = document?.ObjectsManual is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : document.ObjectsManual.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value?.ToJson() ?? string.Empty,
                StringComparer.Ordinal);

        return new IntentEnvelope(userId, manual, document.ToStableJson());
    }

}
