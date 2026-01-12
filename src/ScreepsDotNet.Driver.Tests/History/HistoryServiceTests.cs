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

    [Fact]
    public async Task UploadRoomHistoryChunkAsync_PersistsDocument()
    {
        var config = new DriverConfig(new FakeEnvironmentService());
        var service = new HistoryService(config, fixture.RedisProvider, fixture.MongoProvider);

        await service.SaveRoomHistoryAsync("W2N3", 200, """{"energy":300}""");
        await service.SaveRoomHistoryAsync("W2N3", 201, """{"energy":250}""");
        await service.UploadRoomHistoryChunkAsync("W2N3", 200);

        var collection = fixture.GetCollection<RoomHistoryChunkDocument>(fixture.Options.RoomHistoryCollection);
        var document = await collection.Find(doc => doc.Room == "W2N3" && doc.BaseTick == 200)
                                       .FirstOrDefaultAsync();
        Assert.NotNull(document);
        Assert.Equal("W2N3:200", document!.Id);
        Assert.True(document.Ticks.ContainsKey("200"));
        Assert.True(document.Ticks.ContainsKey("201"));
    }
}
