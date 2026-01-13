using MongoDB.Driver;
using ScreepsDotNet.Driver.Abstractions.Eventing;
using ScreepsDotNet.Driver.Services;
using ScreepsDotNet.Driver.Services.History;
using ScreepsDotNet.Driver.Tests.TestSupport;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

namespace ScreepsDotNet.Driver.Tests.History;

public sealed class HistoryServiceTests(MongoRedisFixture fixture) : IClassFixture<MongoRedisFixture>
{
    [Fact]
    public async Task UploadRoomHistoryChunkAsync_RaisesEvent()
    {
        var config = new DriverConfig(new FakeEnvironmentService());
        var service = new HistoryService(config, fixture.RedisProvider, fixture.MongoProvider);
        var received = new TaskCompletionSource<RoomHistorySavedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var token = TestContext.Current.CancellationToken;
        config.RoomHistorySaved += (_, args) => received.TrySetResult(args);

        await service.SaveRoomHistoryAsync("W1N1", 100, """{"hits":100}""", token);
        await service.SaveRoomHistoryAsync("W1N1", 101, """{"hits":80}""", token);

        await service.UploadRoomHistoryChunkAsync("W1N1", 100, token);

        var args = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        Assert.Equal("W1N1", args.RoomName);
        Assert.Equal(100, args.BaseGameTime);
        Assert.True(args.Chunk.Ticks.ContainsKey(100));
        Assert.True(args.Chunk.Ticks.ContainsKey(101));
    }

    [Fact]
    public async Task UploadRoomHistoryChunkAsync_PersistsDocument()
    {
        var config = new DriverConfig(new FakeEnvironmentService());
        var service = new HistoryService(config, fixture.RedisProvider, fixture.MongoProvider);
        var token = TestContext.Current.CancellationToken;

        await service.SaveRoomHistoryAsync("W2N3", 200, """{"energy":300}""", token);
        await service.SaveRoomHistoryAsync("W2N3", 201, """{"energy":250}""", token);
        await service.UploadRoomHistoryChunkAsync("W2N3", 200, token);

        var collection = fixture.GetCollection<RoomHistoryChunkDocument>(fixture.Options.RoomHistoryCollection);
        var document = await collection.Find(doc => doc.Room == "W2N3" && doc.BaseTick == 200)
                                       .FirstOrDefaultAsync(token);
        Assert.NotNull(document);
        Assert.Equal("W2N3:200", document!.Id);
        Assert.True(document.Ticks.ContainsKey("200"));
        Assert.True(document.Ticks.ContainsKey("201"));
    }
}
