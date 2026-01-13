using System.Collections.Concurrent;
using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Driver.Contracts;

namespace ScreepsDotNet.Driver.Services.Rooms;

internal sealed class RoomSnapshotProvider(IRoomSnapshotBuilder builder) : IRoomSnapshotProvider
{
    private readonly ConcurrentDictionary<string, SnapshotCacheEntry> _cache = new(StringComparer.Ordinal);

    public Task<RoomSnapshot> GetSnapshotAsync(string roomName, int gameTime, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);

        while (true)
        {
            if (_cache.TryGetValue(roomName, out var entry) && entry.GameTime == gameTime) return entry.Value.Value;

            var newEntry = new SnapshotCacheEntry(gameTime, new Lazy<Task<RoomSnapshot>>(() => builder.BuildAsync(roomName, gameTime, token), LazyThreadSafetyMode.ExecutionAndPublication));

            if (entry is null)
            {
                if (_cache.TryAdd(roomName, newEntry))
                    return newEntry.Value.Value;

                continue;
            }

            if (_cache.TryUpdate(roomName, newEntry, entry))
                return newEntry.Value.Value;
        }
    }

    public void Invalidate(string roomName)
    {
        if (string.IsNullOrWhiteSpace(roomName)) return;
        _cache.TryRemove(roomName, out _);
    }

    private sealed record SnapshotCacheEntry(int GameTime, Lazy<Task<RoomSnapshot>> Value);
}
