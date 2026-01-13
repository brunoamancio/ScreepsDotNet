using ScreepsDotNet.Driver.Contracts;

namespace ScreepsDotNet.Driver.Abstractions.Rooms;

public interface IRoomSnapshotProvider
{
    Task<RoomSnapshot> GetSnapshotAsync(string roomName, int gameTime, CancellationToken token = default);
    void Invalidate(string roomName);
}
