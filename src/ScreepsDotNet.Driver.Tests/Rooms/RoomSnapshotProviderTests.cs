using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Driver.Services.Rooms;

namespace ScreepsDotNet.Driver.Tests.Rooms;

public sealed class RoomSnapshotProviderTests
{
    [Fact]
    public async Task GetSnapshotAsync_CachesSnapshotPerTick()
    {
        var builder = new StubSnapshotBuilder();
        var provider = new RoomSnapshotProvider(builder);

        var first = await provider.GetSnapshotAsync("W0N0", 123, CancellationToken.None);
        var second = await provider.GetSnapshotAsync("W0N0", 123, CancellationToken.None);

        Assert.Same(first, second);
        var call = Assert.Single(builder.Calls);
        Assert.Equal(("W0N0", 123), call);
    }

    [Fact]
    public async Task GetSnapshotAsync_DifferentTicksOrInvalidation_Refetches()
    {
        var builder = new StubSnapshotBuilder();
        var provider = new RoomSnapshotProvider(builder);

        var first = await provider.GetSnapshotAsync("W0N0", 200, CancellationToken.None);
        provider.Invalidate("W0N0");
        builder.NextSnapshot = CreateSnapshot("W0N0", 201);
        var second = await provider.GetSnapshotAsync("W0N0", 201, CancellationToken.None);

        Assert.NotSame(first, second);
        Assert.Equal(2, builder.Calls.Count);
        Assert.Equal(("W0N0", 200), builder.Calls[0]);
        Assert.Equal(("W0N0", 201), builder.Calls[1]);
    }

    private sealed class StubSnapshotBuilder : IRoomSnapshotBuilder
    {
        public List<(string Room, int GameTime)> Calls { get; } = [];
        public RoomSnapshot NextSnapshot { get; set; } = CreateSnapshot("default", 0);

        public Task<RoomSnapshot> BuildAsync(string roomName, int gameTime, CancellationToken token = default)
        {
            Calls.Add((roomName, gameTime));
            return Task.FromResult(NextSnapshot);
        }
    }

    private static RoomSnapshot CreateSnapshot(string room, int gameTime)
        => new(
            room,
            gameTime,
            null,
            new Dictionary<string, RoomObjectState>(),
            new Dictionary<string, UserState>(),
            null,
            new Dictionary<string, RoomTerrainSnapshot>(),
            [],
            string.Empty);
}
