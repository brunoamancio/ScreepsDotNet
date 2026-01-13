using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

namespace ScreepsDotNet.Driver.Abstractions.Rooms;

public interface IRoomDataService
{
    Task<IReadOnlyList<string>> DrainActiveRoomsAsync(CancellationToken token = default);
    Task ActivateRoomsAsync(IEnumerable<string> roomNames, CancellationToken token = default);

    Task<RoomObjectsPayload> GetRoomObjectsAsync(string roomName, CancellationToken token = default);
    Task<IReadOnlyList<RoomFlagDocument>> GetRoomFlagsAsync(string roomName, CancellationToken token = default);
    Task<IReadOnlyDictionary<string, RoomTerrainDocument>> GetRoomTerrainAsync(string roomName, CancellationToken token = default);

    Task<RoomDocument?> GetRoomInfoAsync(string roomName, CancellationToken token = default);
    Task SaveRoomInfoAsync(RoomDocument room, CancellationToken token = default);
    Task SetRoomStatusAsync(string roomName, string status, CancellationToken token = default);

    Task<RoomIntentDocument?> GetRoomIntentsAsync(string roomName, CancellationToken token = default);
    Task ClearRoomIntentsAsync(string roomName, CancellationToken token = default);

    Task SaveRoomEventLogAsync(string roomName, string eventLogJson, CancellationToken token = default);
    Task SaveMapViewAsync(string roomName, string mapViewJson, CancellationToken token = default);

    Task UpdateAccessibleRoomsListAsync(CancellationToken token = default);
    Task UpdateRoomStatusDataAsync(CancellationToken token = default);

    Task<InterRoomSnapshot> GetInterRoomSnapshotAsync(int gameTime, CancellationToken token = default);
}

public sealed record RoomObjectsPayload(
    IReadOnlyDictionary<string, RoomObjectDocument> Objects,
    IReadOnlyDictionary<string, UserDocument> Users);

public sealed record InterRoomSnapshot(
    int GameTime,
    IReadOnlyList<RoomObjectDocument> MovingCreeps,
    IReadOnlyDictionary<string, RoomDocument> AccessibleRooms,
    IReadOnlyList<RoomObjectDocument> SpecialRoomObjects,
    InterRoomMarketSnapshot Market);

public sealed record InterRoomMarketSnapshot(
    IReadOnlyList<MarketOrderDocument> Orders,
    IReadOnlyList<UserDocument> Users,
    IReadOnlyList<PowerCreepDocument> UserPowerCreeps,
    IReadOnlyList<UserIntentDocument> UserIntents,
    string ShardName);
