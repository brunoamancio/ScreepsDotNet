namespace ScreepsDotNet.Driver.Tests.TestDoubles;

using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

internal class RoomDataServiceDouble : IRoomDataService
{
    public virtual Task<IReadOnlyList<string>> DrainActiveRoomsAsync(CancellationToken token = default)
        => Task.FromResult<IReadOnlyList<string>>([]);

    public virtual Task ActivateRoomsAsync(IEnumerable<string> roomNames, CancellationToken token = default)
        => Task.CompletedTask;

    public virtual Task<RoomObjectsPayload> GetRoomObjectsAsync(string roomName, CancellationToken token = default)
        => Task.FromResult(new RoomObjectsPayload(new Dictionary<string, RoomObjectDocument>(), new Dictionary<string, UserDocument>()));

    public virtual Task<IReadOnlyList<RoomFlagDocument>> GetRoomFlagsAsync(string roomName, CancellationToken token = default)
        => Task.FromResult<IReadOnlyList<RoomFlagDocument>>([]);

    public virtual Task<IReadOnlyDictionary<string, RoomTerrainDocument>> GetRoomTerrainAsync(string roomName, CancellationToken token = default)
        => Task.FromResult<IReadOnlyDictionary<string, RoomTerrainDocument>>(new Dictionary<string, RoomTerrainDocument>());

    public virtual Task<RoomDocument?> GetRoomInfoAsync(string roomName, CancellationToken token = default)
        => Task.FromResult<RoomDocument?>(null);

    public virtual Task SaveRoomInfoAsync(RoomDocument room, CancellationToken token = default)
        => Task.CompletedTask;

    public virtual Task SetRoomStatusAsync(string roomName, string status, CancellationToken token = default)
        => Task.CompletedTask;

    public virtual Task<RoomIntentDocument?> GetRoomIntentsAsync(string roomName, CancellationToken token = default)
        => Task.FromResult<RoomIntentDocument?>(null);

    public virtual Task ClearRoomIntentsAsync(string roomName, CancellationToken token = default)
        => Task.CompletedTask;

    public virtual Task SaveRoomEventLogAsync(string roomName, string eventLogJson, CancellationToken token = default)
        => Task.CompletedTask;

    public virtual Task SaveMapViewAsync(string roomName, string mapViewJson, CancellationToken token = default)
        => Task.CompletedTask;

    public virtual Task UpdateAccessibleRoomsListAsync(CancellationToken token = default)
        => Task.CompletedTask;

    public virtual Task UpdateRoomStatusDataAsync(CancellationToken token = default)
        => Task.CompletedTask;

    public virtual Task<InterRoomSnapshot> GetInterRoomSnapshotAsync(int gameTime, CancellationToken token = default)
        => Task.FromResult(new InterRoomSnapshot(
            gameTime,
            [],
            new Dictionary<string, RoomDocument>(),
            new Dictionary<string, RoomExitTopology>(),
            [],
            new InterRoomMarketSnapshot(
                [],
                [],
                [],
                [],
                string.Empty)));
}
