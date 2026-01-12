using ScreepsDotNet.Driver.Abstractions.Eventing;
using ScreepsDotNet.Driver.Services;
using ScreepsDotNet.Driver.Services.History;
using ScreepsDotNet.Driver.Tests.TestSupport;

namespace ScreepsDotNet.Driver.Tests.History;

public sealed class HistoryServiceTests(MongoRedisFixture fixture) : IClassFixture<MongoRedisFixture>
{
    private readonly MongoRedisFixture _fixture = fixture;

    [Fact]
    public async Task UploadRoomHistoryChunkAsync_RaisesEvent()
    {
        var config = new DriverConfig(new FakeEnvironmentService());
        var service = new HistoryService(config, _fixture.RedisProvider);
        var received = new TaskCompletionSource<RoomHistorySavedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        config.RoomHistorySaved += (_, args) => received.TrySetResult(args);

        await service.SaveRoomHistoryAsync("W1N1", 100, """{"hits":100}""");
        await service.SaveRoomHistoryAsync("W1N1", 101, """{"hits":80}""");

        await service.UploadRoomHistoryChunkAsync("W1N1", 100);

        var args = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("W1N1", args.RoomName);
        Assert.Equal(100, args.BaseGameTime);
        Assert.True(args.Chunk.Ticks.ContainsKey(100));
        Assert.True(args.Chunk.Ticks.ContainsKey(101));
    }
}
