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
            MapRoomInfo(roomInfo),
            MapRoomObjects(objectsPayload.Objects),
            MapUsers(objectsPayload.Users),
            MapRoomIntents(intents),
            MapTerrain(terrain),
            MapFlags(flags),
            roomInfo.ToStableJson());
    }

    private static IReadOnlyDictionary<string, RoomObjectState> MapRoomObjects(IReadOnlyDictionary<string, RoomObjectDocument> objects)
    {
        var result = new Dictionary<string, RoomObjectState>(objects.Count, StringComparer.Ordinal);
        foreach (var (_, document) in objects)
        {
            if (document is null) continue;
            var state = MapRoomObject(document);
            result[state.Id] = state;
        }

        return result;
    }

    private static RoomObjectState MapRoomObject(RoomObjectDocument document)
    {
        var store = document.Store is null ? EmptyIntDictionary : new Dictionary<string, int>(document.Store, StringComparer.Ordinal);
        var storeCapacityResource = document.StoreCapacityResource is null
            ? EmptyIntDictionary
            : new Dictionary<string, int>(document.StoreCapacityResource, StringComparer.Ordinal);

        return new RoomObjectState(
            document.Id.ToString(),
            document.Type ?? string.Empty,
            document.Room ?? string.Empty,
            document.Shard,
            document.UserId,
            document.X ?? 0,
            document.Y ?? 0,
            document.Hits,
            document.HitsMax,
            document.Fatigue,
            document.TicksToLive,
            document.Name,
            document.Level,
            document.Density,
            document.MineralType,
            document.DepositType,
            document.StructureType,
            store,
            document.StoreCapacity,
            storeCapacityResource,
            MapReservation(document.Reservation),
            MapSign(document.Sign),
            MapStructure(document.Structure),
            MapEffects(document.Effects),
            document.ToStableJson());
    }

    private static RoomReservationSnapshot? MapReservation(RoomReservationDocument? document)
        => document is null ? null : new RoomReservationSnapshot(document.UserId, document.EndTime);

    private static RoomSignSnapshot? MapSign(RoomSignDocument? document)
        => document is null ? null : new RoomSignSnapshot(document.UserId, document.Text, document.Time);

    private static RoomObjectStructureSnapshot? MapStructure(RoomObjectStructureDocument? document)
        => document is null
            ? null
            : new RoomObjectStructureSnapshot(document.Id, document.Type, document.UserId, document.Hits, document.HitsMax);

    private static IReadOnlyDictionary<string, object?> MapEffects(BsonArray? effects)
    {
        if (effects is null || effects.Count == 0)
            return EmptyObjectDictionary;

        var result = new Dictionary<string, object?>(effects.Count, StringComparer.Ordinal);
        for (var i = 0; i < effects.Count; i++)
        {
            var effect = effects[i];
            result[i.ToString()] = effect;
        }
        return result;
    }

    private static IReadOnlyDictionary<string, UserState> MapUsers(IReadOnlyDictionary<string, UserDocument> users)
    {
        var result = new Dictionary<string, UserState>(users.Count, StringComparer.Ordinal);
        foreach (var (id, document) in users)
        {
            if (document is null || string.IsNullOrWhiteSpace(id)) continue;
            result[id] = new UserState(
                id,
                document.Username ?? id,
                document.Cpu ?? 0,
                document.Power ?? 0,
                document.Money ?? 0,
                document.Active.GetValueOrDefault() != 0,
                document.ToStableJson());
        }
        return result;
    }

    private static RoomInfoSnapshot? MapRoomInfo(RoomDocument? room)
        => room is null
            ? null
            : new RoomInfoSnapshot(
                room.Id,
                room.Shard,
                room.Status,
                room.Novice,
                room.RespawnArea,
                room.OpenTime,
                room.Owner,
                room.Controller?.Level,
                room.EnergyAvailable,
                room.NextNpcMarketOrder,
                room.PowerBankTime,
                room.InvaderGoal,
                room.ToStableJson());

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
        if (documents.Count == 0) return Array.Empty<RoomFlagSnapshot>();
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

    private static readonly IReadOnlyDictionary<string, int> EmptyIntDictionary = new Dictionary<string, int>(0);
    private static readonly IReadOnlyDictionary<string, object?> EmptyObjectDictionary = new Dictionary<string, object?>(0);
}
