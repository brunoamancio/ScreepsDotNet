using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScreepsDotNet.Driver.Abstractions.Config;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Constants;

namespace ScreepsDotNet.Driver.Services.Loops;

internal sealed class RoomsDoneBroadcaster(IDriverConfig config, IOptions<RoomsDoneBroadcastOptions> options, ILogger<RoomsDoneBroadcaster>? logger = null)
    : IRoomsDoneBroadcaster
{
    private readonly RoomsDoneBroadcastOptions _options = options.Value;
    private DateTimeOffset _lastBroadcast;

    public Task PublishAsync(int gameTime, CancellationToken token = default)
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastBroadcast < _options.MinInterval)
            return Task.CompletedTask;

        _lastBroadcast = now;
        config.Emit(DriverEventNames.RoomsDone, gameTime);
        logger?.LogDebug("Broadcasted roomsDone event for tick {GameTime} (interval {Interval}).", gameTime, _options.MinInterval);
        return Task.CompletedTask;
    }
}
