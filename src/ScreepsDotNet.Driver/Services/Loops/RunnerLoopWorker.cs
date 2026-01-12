using Microsoft.Extensions.Logging;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Runtime;

namespace ScreepsDotNet.Driver.Services.Loops;

internal sealed class RunnerLoopWorker(IRuntimeCoordinator coordinator, ILogger<RunnerLoopWorker>? logger = null)
    : IRunnerLoopWorker
{
    private readonly IRuntimeCoordinator _coordinator = coordinator;
    private readonly ILogger<RunnerLoopWorker>? _logger = logger;

    public async Task HandleUserAsync(string userId, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        try
        {
            await _coordinator.ExecuteAsync(userId, token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Runner loop encountered an error while executing user {UserId}.", userId);
        }
    }
}
