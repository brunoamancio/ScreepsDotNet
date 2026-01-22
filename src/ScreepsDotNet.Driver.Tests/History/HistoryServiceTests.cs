using MongoDB.Driver;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Abstractions.Eventing;
using ScreepsDotNet.Driver.Constants;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Driver.Services;
using ScreepsDotNet.Driver.Services.History;
using ScreepsDotNet.Driver.Tests.TestSupport;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

namespace ScreepsDotNet.Driver.Tests.History;

[Trait("Category", "Integration")]
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

        await service.SaveRoomHistoryAsync("W1N1", 100, CreateEmptyPayload("W1N1"), token);
        await service.SaveRoomHistoryAsync("W1N1", 101, CreateEmptyPayload("W1N1"), token);

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

        await service.SaveRoomHistoryAsync("W2N3", 200, CreateEmptyPayload("W2N3"), token);
        await service.SaveRoomHistoryAsync("W2N3", 201, CreateEmptyPayload("W2N3"), token);
        await service.UploadRoomHistoryChunkAsync("W2N3", 200, token);

        var collection = fixture.GetCollection<RoomHistoryChunkDocument>(fixture.Options.RoomHistoryCollection);
        var document = await collection.Find(doc => doc.Room == "W2N3" && doc.BaseTick == 200)
                                       .FirstOrDefaultAsync(token);
        Assert.NotNull(document);
        Assert.Equal("W2N3:200", document!.Id);
        Assert.True(document.Ticks.ContainsKey("200"));
        Assert.True(document.Ticks.ContainsKey("201"));
    }

    [Fact]
    public async Task RoomStatsUpdater_FlushAsync_EmitsTypedPayload()
    {
        var config = new DriverConfig(new FakeEnvironmentService());
        var service = new HistoryService(config, fixture.RedisProvider, fixture.MongoProvider);
        var token = TestContext.Current.CancellationToken;
        var received = new TaskCompletionSource<RoomStatsUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);

        config.ProcessorLoopStage += (_, args) => {
            if (!string.Equals(args.Stage, LoopStageNames.Processor.RoomStatsUpdated, StringComparison.Ordinal))
                return;
            if (args.Payload is RoomStatsUpdate update)
                received.TrySetResult(update);
        };

        var updater = service.CreateRoomStatsUpdater("W3N5");
        updater.Increment("user1", RoomStatsMetricNames.SpawnsCreate, 2);
        await updater.FlushAsync(321, token);

        var payload = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        Assert.Equal("W3N5", payload.Room);
        Assert.Equal(321, payload.GameTime);
        Assert.True(payload.Metrics.TryGetValue("user1", out var metrics));
        Assert.Equal(2, metrics[RoomStatsMetricNames.SpawnsCreate]);

        // Ensure subsequent increments produce fresh snapshots.
        var secondReceived = new TaskCompletionSource<RoomStatsUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
        config.ProcessorLoopStage += (_, args) => {
            if (!string.Equals(args.Stage, LoopStageNames.Processor.RoomStatsUpdated, StringComparison.Ordinal))
                return;
            if (args.Payload is RoomStatsUpdate update && update.GameTime == 400)
                secondReceived.TrySetResult(update);
        };

        updater.Increment("user1", RoomStatsMetricNames.SpawnsRenew, 1);
        await updater.FlushAsync(400, token);

        var secondPayload = await secondReceived.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        Assert.Equal(1, secondPayload.Metrics["user1"][RoomStatsMetricNames.SpawnsRenew]);
        Assert.False(secondPayload.Metrics["user1"].ContainsKey(RoomStatsMetricNames.SpawnsCreate));
    }
    private static RoomHistoryTickPayload CreateEmptyPayload(string room)
        => new(room,
            new Dictionary<string, RoomObjectSnapshot>(),
            new Dictionary<string, UserState>());
}
