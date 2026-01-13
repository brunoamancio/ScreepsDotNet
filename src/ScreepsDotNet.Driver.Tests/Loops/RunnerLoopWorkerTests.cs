using ScreepsDotNet.Driver.Abstractions.Runtime;
using ScreepsDotNet.Driver.Services.Loops;

namespace ScreepsDotNet.Driver.Tests.Loops;

public sealed class RunnerLoopWorkerTests
{
    [Fact]
    public async Task HandleUserAsync_ForwardsQueueDepthToCoordinator()
    {
        var coordinator = new RecordingCoordinator();
        var worker = new RunnerLoopWorker(coordinator);

        await worker.HandleUserAsync("user123", 42, CancellationToken.None);

        Assert.Equal("user123", coordinator.LastUserId);
        Assert.Equal(42, coordinator.LastQueueDepth);
    }

    private sealed class RecordingCoordinator : IRuntimeCoordinator
    {
        public string? LastUserId { get; private set; }
        public int? LastQueueDepth { get; private set; }

        public Task ExecuteAsync(string userId, int? queueDepth = null, CancellationToken token = default)
        {
            LastUserId = userId;
            LastQueueDepth = queueDepth;
            return Task.CompletedTask;
        }
    }
}
