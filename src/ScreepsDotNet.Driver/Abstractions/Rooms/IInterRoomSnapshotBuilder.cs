namespace ScreepsDotNet.Driver.Abstractions.Rooms;

using ScreepsDotNet.Driver.Contracts;

public interface IInterRoomSnapshotBuilder
{
    Task<GlobalSnapshot> BuildAsync(int gameTime, CancellationToken token = default);
}
