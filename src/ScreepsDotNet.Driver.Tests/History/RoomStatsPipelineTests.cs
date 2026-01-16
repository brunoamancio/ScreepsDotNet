using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Abstractions.History;
using ScreepsDotNet.Driver.Constants;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Driver.Services;
using ScreepsDotNet.Driver.Services.History;
using ScreepsDotNet.Driver.Tests.TestSupport;

namespace ScreepsDotNet.Driver.Tests.History;

public sealed class RoomStatsPipelineTests
{
    [Fact]
    public async Task Pipeline_PersistsUpdates()
    {
        var config = new DriverConfig(new FakeEnvironmentService());
        var repository = new RecordingRoomStatsRepository();

        using var pipeline = new RoomStatsPipeline(config, repository);

        var metrics = new Dictionary<string, IReadOnlyDictionary<string, int>>
        {
            ["user1"] = new Dictionary<string, int> { [RoomStatsMetricNames.SpawnsCreate] = 3 }
        };
        var update = new RoomStatsUpdate("W7N7", 900, metrics);

        config.EmitProcessorLoopStage(LoopStageNames.Processor.RoomStatsUpdated, update);

        var recorded = await repository.Updates.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal("W7N7", recorded.Room);
        Assert.Equal(900, recorded.GameTime);
        Assert.Equal(3, recorded.Metrics["user1"][RoomStatsMetricNames.SpawnsCreate]);
    }

    private sealed class RecordingRoomStatsRepository : IRoomStatsRepository
    {
        public TaskCompletionSource<RoomStatsUpdate> Updates { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task AppendAsync(RoomStatsUpdate update, CancellationToken token = default)
        {
            Updates.TrySetResult(update);
            return Task.CompletedTask;
        }
    }
}
