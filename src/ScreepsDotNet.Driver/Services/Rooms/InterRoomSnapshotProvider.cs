namespace ScreepsDotNet.Driver.Services.Rooms;

using ScreepsDotNet.Driver.Abstractions.Environment;
using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Driver.Contracts;

internal sealed class InterRoomSnapshotProvider(
    IInterRoomSnapshotBuilder builder,
    IEnvironmentService environment) : IInterRoomSnapshotProvider
{
    private GlobalSnapshot? _cachedSnapshot;
    private int? _cachedGameTime;
    private readonly Lock _gate = new();

    public async Task<GlobalSnapshot> GetSnapshotAsync(CancellationToken token = default)
    {
        var gameTime = await environment.GetGameTimeAsync(token).ConfigureAwait(false);
        if (TryGetCached(gameTime, out var cached))
            return cached;

        var snapshot = await builder.BuildAsync(gameTime, token).ConfigureAwait(false);
        if (snapshot.GameTime == gameTime)
            Cache(snapshot);

        return snapshot;
    }

    public void Invalidate()
    {
        lock (_gate)
        {
            _cachedSnapshot = null;
            _cachedGameTime = null;
        }
    }

    private bool TryGetCached(int gameTime, out GlobalSnapshot snapshot)
    {
        lock (_gate)
        {
            if (_cachedSnapshot is not null && _cachedGameTime == gameTime)
            {
                snapshot = _cachedSnapshot;
                return true;
            }
        }

        snapshot = null!;
        return false;
    }

    private void Cache(GlobalSnapshot snapshot)
    {
        lock (_gate)
        {
            _cachedSnapshot = snapshot;
            _cachedGameTime = snapshot.GameTime;
        }
    }
}
