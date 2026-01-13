namespace ScreepsDotNet.Engine.Data.GlobalState;

using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Engine.Data.Models;

internal sealed class GlobalStateProvider(IInterRoomSnapshotProvider snapshotProvider) : IGlobalStateProvider
{
    public async Task<GlobalState> GetGlobalStateAsync(CancellationToken token = default)
    {
        var snapshot = await snapshotProvider.GetSnapshotAsync(token).ConfigureAwait(false);
        return GlobalState.FromSnapshot(snapshot);
    }

    public void Invalidate()
        => snapshotProvider.Invalidate();
}
