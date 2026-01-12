namespace ScreepsDotNet.Driver.Abstractions.History;

public interface IHistoryService
{
    Task SaveRoomHistoryAsync(string roomName, int gameTime, string serializedObjects, CancellationToken token = default);
    Task UploadRoomHistoryChunkAsync(string roomName, int baseGameTime, CancellationToken token = default);

    IRoomStatsUpdater CreateRoomStatsUpdater(string roomName);
}

public interface IRoomStatsUpdater
{
    void Increment(string userId, string metric, int amount);
    Task FlushAsync(CancellationToken token = default);
}
