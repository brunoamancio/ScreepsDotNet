using Microsoft.Extensions.Logging;
using ScreepsDotNet.Driver.Abstractions.Loops;

namespace ScreepsDotNet.Driver.Services.Loops;

internal sealed class NoopMainLoopGlobalProcessor(ILogger<NoopMainLoopGlobalProcessor>? logger = null) : IMainLoopGlobalProcessor
{
    private readonly ILogger<NoopMainLoopGlobalProcessor>? _logger = logger;
    private static readonly TimeSpan WarningThrottle = TimeSpan.FromSeconds(60);
    private DateTimeOffset _lastWarningUtc = DateTimeOffset.MinValue;

    public Task ExecuteAsync(CancellationToken token = default)
    {
        if (DateTimeOffset.UtcNow - _lastWarningUtc >= WarningThrottle)
        {
            _logger?.LogInformation("Main loop global stage is not configured. Skipping until a processor is registered.");
            _lastWarningUtc = DateTimeOffset.UtcNow;
        }

        return Task.CompletedTask;
    }
}
