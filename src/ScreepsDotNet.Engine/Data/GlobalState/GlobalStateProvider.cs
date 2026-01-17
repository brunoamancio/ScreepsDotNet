namespace ScreepsDotNet.Engine.Data.GlobalState;

using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Engine.Data.Models;

internal sealed class GlobalStateProvider(IInterRoomSnapshotProvider snapshotProvider) : IGlobalStateProvider
{
    private readonly Lock _gate = new();
    private GlobalState? _cachedState;
    private int? _cachedGameTime;

    public async Task<GlobalState> GetGlobalStateAsync(int gameTime, CancellationToken token = default)
    {
        if (TryGetCached(gameTime, out var cached))
            return cached;

        var snapshot = await snapshotProvider.GetSnapshotAsync(token).ConfigureAwait(false);
        var state = GlobalState.FromSnapshot(snapshot);
        if (state.GameTime == gameTime)
            Cache(state);
        return state;
    }

    public void Invalidate()
    {
        lock (_gate) {
            _cachedState = null;
            _cachedGameTime = null;
        }
        snapshotProvider.Invalidate();
    }

    private bool TryGetCached(int gameTime, out GlobalState state)
    {
        lock (_gate) {
            if (_cachedState is not null && _cachedGameTime == gameTime) {
                state = _cachedState;
                return true;
            }
        }

        state = null!;
        return false;
    }

    private void Cache(GlobalState state)
    {
        lock (_gate) {
            _cachedState = state;
            _cachedGameTime = state.GameTime;
        }
    }
}
