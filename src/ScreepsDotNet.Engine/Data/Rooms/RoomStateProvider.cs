namespace ScreepsDotNet.Engine.Data.Rooms;

using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Engine.Data.Models;

internal sealed class RoomStateProvider(IRoomSnapshotProvider snapshotProvider) : IRoomStateProvider
{
    public async Task<RoomState> GetRoomStateAsync(string roomName, int gameTime, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);
        var snapshot = await snapshotProvider.GetSnapshotAsync(roomName, gameTime, token).ConfigureAwait(false);
        return RoomState.FromSnapshot(snapshot);
    }

    public void Invalidate(string roomName)
    {
        if (string.IsNullOrWhiteSpace(roomName))
            return;

        snapshotProvider.Invalidate(roomName);
    }
}
