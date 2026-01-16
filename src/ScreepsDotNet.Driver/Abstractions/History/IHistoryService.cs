namespace ScreepsDotNet.Driver.Abstractions.History;

using ScreepsDotNet.Driver.Contracts;

public interface IHistoryService
{
    Task SaveRoomHistoryAsync(string roomName, int gameTime, RoomHistoryTickPayload payload, CancellationToken token = default);
    Task UploadRoomHistoryChunkAsync(string roomName, int baseGameTime, CancellationToken token = default);

    IRoomStatsUpdater CreateRoomStatsUpdater(string roomName);
}

public interface IRoomStatsUpdater
{
    void Increment(string userId, string metric, int amount);
    Task FlushAsync(int gameTime, CancellationToken token = default);
}
