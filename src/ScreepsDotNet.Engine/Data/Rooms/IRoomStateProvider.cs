namespace ScreepsDotNet.Engine.Data.Rooms;

using ScreepsDotNet.Engine.Data.Models;

public interface IRoomStateProvider
{
    Task<RoomState> GetRoomStateAsync(string roomName, int gameTime, CancellationToken token = default);
    void Invalidate(string roomName);
}
