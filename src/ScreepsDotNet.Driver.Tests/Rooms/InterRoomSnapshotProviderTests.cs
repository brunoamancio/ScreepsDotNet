using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Driver.Services.Rooms;
using ScreepsDotNet.Driver.Tests.TestSupport;

namespace ScreepsDotNet.Driver.Tests.Rooms;

public sealed class InterRoomSnapshotProviderTests
{
    [Fact]
    public async Task GetSnapshotAsync_CachesPerGameTime()
    {
        var token = TestContext.Current.CancellationToken;
        var builder = new StubBuilder();
        var environment = new FakeEnvironmentService();
        await environment.IncrementGameTimeAsync(token);
        var provider = new InterRoomSnapshotProvider(builder, environment);

        var first = await provider.GetSnapshotAsync(token);
        var second = await provider.GetSnapshotAsync(token);

        Assert.Same(first, second);
        Assert.Single(builder.Calls);
        Assert.Equal(1, builder.Calls[0]);
    }

    [Fact]
    public async Task GetSnapshotAsync_RebuildsWhenGameTimeChanges()
    {
        var token = TestContext.Current.CancellationToken;
        var builder = new StubBuilder();
        var environment = new FakeEnvironmentService();
        await environment.IncrementGameTimeAsync(token); // 1
        var provider = new InterRoomSnapshotProvider(builder, environment);

        var first = await provider.GetSnapshotAsync(token);
        await environment.IncrementGameTimeAsync(token); // 2
        var second = await provider.GetSnapshotAsync(token);

        Assert.NotSame(first, second);
        Assert.Equal(2, builder.Calls.Count);
        Assert.Equal(1, builder.Calls[0]);
        Assert.Equal(2, builder.Calls[1]);
    }

    [Fact]
    public async Task Invalidate_ForcesRebuildForSameTick()
    {
        var token = TestContext.Current.CancellationToken;
        var builder = new StubBuilder();
        var environment = new FakeEnvironmentService();
        await environment.IncrementGameTimeAsync(token); // 1
        var provider = new InterRoomSnapshotProvider(builder, environment);

        var first = await provider.GetSnapshotAsync(token);
        provider.Invalidate();
        var second = await provider.GetSnapshotAsync(token);

        Assert.NotSame(first, second);
        Assert.Equal(2, builder.Calls.Count);
    }

    private sealed class StubBuilder : IInterRoomSnapshotBuilder
    {
        public List<int> Calls { get; } = [];

        public Task<GlobalSnapshot> BuildAsync(int gameTime, CancellationToken token = default)
        {
            Calls.Add(gameTime);
            return Task.FromResult(new GlobalSnapshot(
                gameTime,
                [],
                new Dictionary<string, RoomInfoSnapshot>(0, StringComparer.Ordinal),
                [],
                new GlobalMarketSnapshot(
                    [],
                    new Dictionary<string, UserState>(0, StringComparer.Ordinal),
                    [],
                    [],
                    string.Empty)));
        }
    }
}
