using Microsoft.Extensions.Logging;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Runtime;

namespace ScreepsDotNet.Driver.Services.Runtime;

internal sealed class RuntimeTelemetryPipeline : IRuntimeTelemetrySink
{
    private readonly IReadOnlyList<IRuntimeTelemetryListener> _listeners;
    private readonly ILogger<RuntimeTelemetryPipeline>? _logger;

    public RuntimeTelemetryPipeline(IEnumerable<IRuntimeTelemetryListener> listeners, ILogger<RuntimeTelemetryPipeline>? logger = null)
    {
        _listeners = listeners?.ToArray() ?? Array.Empty<IRuntimeTelemetryListener>();
        _logger = logger;
    }

    public Task PublishTelemetryAsync(RuntimeTelemetryPayload payload, CancellationToken token = default)
        => DispatchAsync(listener => listener.OnTelemetryAsync(payload, token));

    public Task PublishWatchdogAlertAsync(RuntimeWatchdogAlert alert, CancellationToken token = default)
        => DispatchAsync(listener => listener.OnWatchdogAlertAsync(alert, token));

    private async Task DispatchAsync(Func<IRuntimeTelemetryListener, Task> dispatcher)
    {
        foreach (var listener in _listeners)
        {
            try
            {
                await dispatcher(listener).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Runtime telemetry listener {Listener} failed.", listener.GetType().Name);
            }
        }
    }
}
