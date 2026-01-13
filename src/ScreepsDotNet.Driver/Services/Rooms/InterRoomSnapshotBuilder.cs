namespace ScreepsDotNet.Driver.Services.Rooms;

using MongoDB.Bson;
using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Driver.Extensions;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

internal sealed class InterRoomSnapshotBuilder(IRoomDataService roomDataService) : IInterRoomSnapshotBuilder
{
    public async Task<GlobalSnapshot> BuildAsync(int gameTime, CancellationToken token = default)
    {
        var snapshot = await roomDataService.GetInterRoomSnapshotAsync(gameTime, token).ConfigureAwait(false);

        var movingCreeps = MapObjects(snapshot.MovingCreeps);
        var accessibleRooms = MapAccessibleRooms(snapshot.AccessibleRooms);
        var specialObjects = MapObjects(snapshot.SpecialRoomObjects);
        var market = MapMarket(snapshot.Market);

        return new GlobalSnapshot(snapshot.GameTime, movingCreeps, accessibleRooms, specialObjects, market);
    }

    private static IReadOnlyList<RoomObjectState> MapObjects(IReadOnlyList<RoomObjectDocument> documents)
    {
        if (documents.Count == 0)
            return [];

        var result = new RoomObjectState[documents.Count];
        for (var i = 0; i < documents.Count; i++)
        {
            var document = documents[i];
            result[i] = RoomContractMapper.MapRoomObject(document);
        }
        return result;
    }

    private static IReadOnlyDictionary<string, RoomInfoSnapshot> MapAccessibleRooms(IReadOnlyDictionary<string, RoomDocument> documents)
    {
        if (documents.Count == 0)
            return new Dictionary<string, RoomInfoSnapshot>(0, StringComparer.Ordinal);

        var result = new Dictionary<string, RoomInfoSnapshot>(documents.Count, StringComparer.Ordinal);
        foreach (var (id, document) in documents)
        {
            if (document is null || string.IsNullOrWhiteSpace(id))
                continue;

            var info = RoomContractMapper.MapRoomInfo(document);
            if (info is not null)
                result[id] = info;
        }

        return result;
    }

    private static GlobalMarketSnapshot MapMarket(InterRoomMarketSnapshot snapshot)
        => new(
            MapOrders(snapshot.Orders),
            MapUsers(snapshot.Users),
            MapPowerCreeps(snapshot.UserPowerCreeps),
            MapUserIntents(snapshot.UserIntents),
            snapshot.ShardName);

    private static IReadOnlyList<MarketOrderSnapshot> MapOrders(IReadOnlyList<MarketOrderDocument> documents)
    {
        if (documents.Count == 0)
            return [];

        var result = new MarketOrderSnapshot[documents.Count];
        for (var i = 0; i < documents.Count; i++)
        {
            var doc = documents[i];
            result[i] = new MarketOrderSnapshot(
                doc.Id.ToString(),
                doc.UserId,
                doc.Type,
                doc.RoomName,
                doc.ResourceType,
                doc.Price,
                doc.Amount,
                doc.RemainingAmount,
                doc.TotalAmount,
                doc.CreatedTick,
                doc.CreatedTimestamp,
                doc.Active,
                doc.ToStableJson());
        }

        return result;
    }

    private static IReadOnlyDictionary<string, UserState> MapUsers(IReadOnlyList<UserDocument> users)
    {
        if (users.Count == 0)
            return new Dictionary<string, UserState>(0, StringComparer.Ordinal);

        var dictionary = new Dictionary<string, UserDocument>(users.Count, StringComparer.Ordinal);
        foreach (var document in users)
        {
            if (document is null || string.IsNullOrWhiteSpace(document.Id))
                continue;

            dictionary[document.Id!] = document;
        }

        return RoomContractMapper.MapUsers(dictionary);
    }

    private static IReadOnlyList<PowerCreepSnapshot> MapPowerCreeps(IReadOnlyList<PowerCreepDocument> documents)
    {
        if (documents.Count == 0)
            return [];

        var result = new PowerCreepSnapshot[documents.Count];
        for (var i = 0; i < documents.Count; i++)
        {
            var doc = documents[i];
            result[i] = new PowerCreepSnapshot(
                doc.Id.ToString(),
                doc.UserId,
                doc.Name,
                doc.ClassName,
                doc.Level,
                doc.HitsMax,
                CopyDictionary(doc.Store),
                doc.StoreCapacity,
                doc.SpawnCooldownTime,
                doc.DeleteTime,
                doc.Shard,
                MapPowers(doc.Powers),
                doc.ToStableJson());
        }

        return result;
    }

    private static IReadOnlyDictionary<string, int> CopyDictionary(Dictionary<string, int>? source)
        => source is null or { Count: 0 }
            ? new Dictionary<string, int>(0)
            : new Dictionary<string, int>(source, StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, PowerCreepPowerSnapshot> MapPowers(Dictionary<string, PowerCreepPowerDocument>? powers)
    {
        if (powers is null || powers.Count == 0)
            return new Dictionary<string, PowerCreepPowerSnapshot>(0, StringComparer.Ordinal);

        var result = new Dictionary<string, PowerCreepPowerSnapshot>(powers.Count, StringComparer.Ordinal);
        foreach (var (id, document) in powers)
        {
            if (string.IsNullOrWhiteSpace(id) || document is null)
                continue;

            result[id] = new PowerCreepPowerSnapshot(document.Level);
        }

        return result;
    }

    private static IReadOnlyList<GlobalUserIntentSnapshot> MapUserIntents(IReadOnlyList<UserIntentDocument> documents)
    {
        if (documents.Count == 0)
            return [];

        var result = new GlobalUserIntentSnapshot[documents.Count];
        for (var i = 0; i < documents.Count; i++)
        {
            var doc = documents[i];
            result[i] = new GlobalUserIntentSnapshot(
                doc.Id.ToString(),
                doc.UserId,
                doc.Intents?.ToJson() ?? string.Empty);
        }

        return result;
    }
}
