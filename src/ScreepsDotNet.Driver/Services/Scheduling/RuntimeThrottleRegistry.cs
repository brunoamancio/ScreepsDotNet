using System.Collections.Concurrent;
using ScreepsDotNet.Driver.Abstractions.Runtime;

namespace ScreepsDotNet.Driver.Services.Scheduling;

internal sealed class RuntimeThrottleRegistry : IRuntimeThrottleRegistry
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _throttledUntil = new(StringComparer.Ordinal);

    public void RegisterThrottle(string userId, TimeSpan duration)
    {
        if (string.IsNullOrWhiteSpace(userId) || duration <= TimeSpan.Zero)
            return;

        var until = DateTimeOffset.UtcNow + duration;
        _throttledUntil[userId] = until;
    }

    public bool TryGetDelay(string userId, out TimeSpan delay)
    {
        delay = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(userId))
            return false;

        if (!_throttledUntil.TryGetValue(userId, out var until))
            return false;

        var remaining = until - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero) {
            _throttledUntil.TryRemove(userId, out _);
            return false;
        }

        delay = remaining;
        return true;
    }

    public void Clear(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return;

        _throttledUntil.TryRemove(userId, out _);
    }
}
