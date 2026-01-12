using Microsoft.Extensions.Logging;
using ScreepsDotNet.Driver.Abstractions.Loops;

namespace ScreepsDotNet.Driver.Services.Loops;

internal sealed class NoopProcessorLoopWorker(ILogger<NoopProcessorLoopWorker>? logger = null) : IProcessorLoopWorker
{
    private readonly ILogger<NoopProcessorLoopWorker>? _logger = logger;
    private static readonly TimeSpan WarningThrottle = TimeSpan.FromSeconds(30);
    private DateTimeOffset _lastWarningUtc = DateTimeOffset.MinValue;

    public Task HandleRoomAsync(string roomName, CancellationToken token = default)
    {
        if (DateTimeOffset.UtcNow - _lastWarningUtc >= WarningThrottle)
        {
            _logger?.LogWarning("Processor loop is active but no worker is configured. Room '{Room}' will be skipped.", roomName);
            _lastWarningUtc = DateTimeOffset.UtcNow;
        }
        return Task.CompletedTask;
    }
}
