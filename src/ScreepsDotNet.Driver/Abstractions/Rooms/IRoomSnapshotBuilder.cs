using ScreepsDotNet.Driver.Contracts;

namespace ScreepsDotNet.Driver.Abstractions.Rooms;

/// <summary>
/// Creates immutable room snapshots for the engine/processor from persisted data.
/// </summary>
public interface IRoomSnapshotBuilder
{
    Task<RoomSnapshot> BuildAsync(string roomName, int gameTime, CancellationToken token = default);
}
