using Microsoft.Extensions.Logging;
using ScreepsDotNet.Driver.Abstractions.Loops;

namespace ScreepsDotNet.Driver.Services.Loops;

internal sealed class NoopRunnerLoopWorker(ILogger<NoopRunnerLoopWorker>? logger = null) : IRunnerLoopWorker
{
    private readonly ILogger<NoopRunnerLoopWorker>? _logger = logger;
    private static readonly TimeSpan WarningThrottle = TimeSpan.FromSeconds(30);
    private DateTimeOffset _lastWarningUtc = DateTimeOffset.MinValue;

    public Task HandleUserAsync(string userId, CancellationToken token = default)
    {
        if (DateTimeOffset.UtcNow - _lastWarningUtc >= WarningThrottle)
        {
            _logger?.LogWarning("Runner loop is active but no worker is configured. User '{UserId}' will be skipped.", userId);
            _lastWarningUtc = DateTimeOffset.UtcNow;
        }
        return Task.CompletedTask;
    }
}
