using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ScreepsDotNet.Driver.Services;
using ScreepsDotNet.Driver.Services.Loops;
using ScreepsDotNet.Driver.Tests.TestSupport;

namespace ScreepsDotNet.Driver.Tests.Loops;

public sealed class RoomsDoneBroadcasterTests
{
    private static readonly int[] ExpectedSequence = [1, 3];

    [Fact]
    public async Task PublishAsync_ThrottlesFrequentEvents()
    {
        var config = new DriverConfig(new FakeEnvironmentService());
        var options = Options.Create(new RoomsDoneBroadcastOptions { MinInterval = TimeSpan.FromMilliseconds(100) });
        var broadcaster = new RoomsDoneBroadcaster(config, options, NullLogger<RoomsDoneBroadcaster>.Instance);

        var received = new List<int>();
        using var subscription = config.Subscribe("roomsDone", args =>
        {
            if (args.Length > 0 && args[0] is int tick)
                received.Add(tick);
        });

        var token = TestContext.Current.CancellationToken;
        await broadcaster.PublishAsync(1, token);
        await broadcaster.PublishAsync(2, token);
        await Task.Delay(150, token);
        await broadcaster.PublishAsync(3, token);

        Assert.Equal(ExpectedSequence, received);
    }
}
