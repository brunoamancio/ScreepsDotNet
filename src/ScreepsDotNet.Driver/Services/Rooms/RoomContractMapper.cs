namespace ScreepsDotNet.Driver.Services.Rooms;

using MongoDB.Bson;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Driver.Extensions;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

internal static class RoomContractMapper
{
    private static readonly IReadOnlyDictionary<string, int> EmptyIntDictionary = new Dictionary<string, int>(0);
    private static readonly IReadOnlyDictionary<string, object?> EmptyObjectDictionary = new Dictionary<string, object?>(0);

    public static IReadOnlyDictionary<string, RoomObjectState> MapRoomObjects(IReadOnlyDictionary<string, RoomObjectDocument> objects)
    {
        var result = new Dictionary<string, RoomObjectState>(objects.Count, StringComparer.Ordinal);
        foreach (var (_, document) in objects)
        {
            var state = MapRoomObject(document);
            result[state.Id] = state;
        }

        return result;
    }

    public static RoomObjectState MapRoomObject(RoomObjectDocument document)
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

    public static IReadOnlyDictionary<string, UserState> MapUsers(IReadOnlyDictionary<string, UserDocument> users)
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

    public static RoomInfoSnapshot? MapRoomInfo(RoomDocument? room)
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
}
