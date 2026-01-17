using Microsoft.Extensions.Logging;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Runtime;

namespace ScreepsDotNet.Driver.Services.Loops;

internal sealed class RunnerLoopWorker(IRuntimeCoordinator coordinator, ILogger<RunnerLoopWorker>? logger = null)
    : IRunnerLoopWorker
{
    public async Task HandleUserAsync(string userId, int? queueDepth, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        try {
            await coordinator.ExecuteAsync(userId, queueDepth, token).ConfigureAwait(false);
        }
        catch (Exception ex) {
            logger?.LogError(ex, "Runner loop encountered an error while executing user {UserId}.", userId);
        }
    }
}
