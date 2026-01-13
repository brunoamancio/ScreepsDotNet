using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Observability;
using ScreepsDotNet.Driver.Abstractions.Runtime;

namespace ScreepsDotNet.Driver.Services.Observability;

internal sealed class ObservabilityTelemetryListener(
    IObservabilityExporter exporter,
    IOptions<ObservabilityOptions> options,
    ILogger<ObservabilityTelemetryListener>? logger = null) : IRuntimeTelemetryListener
{
    private readonly ObservabilityOptions _options = options.Value;
    private readonly IObservabilityExporter _exporter = exporter;
    private readonly ILogger<ObservabilityTelemetryListener>? _logger = logger;

    public Task OnTelemetryAsync(RuntimeTelemetryPayload payload, CancellationToken token = default)
    {
        if (!_options.EnableExporter)
            return Task.CompletedTask;

        try
        {
            return _exporter.ExportTelemetryAsync(payload, token);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Observability exporter failed to handle telemetry for loop {Loop}.", payload.Loop);
            return Task.CompletedTask;
        }
    }

    public Task OnWatchdogAlertAsync(RuntimeWatchdogAlert alert, CancellationToken token = default)
    {
        if (!_options.EnableExporter)
            return Task.CompletedTask;

        try
        {
            return _exporter.ExportWatchdogAlertAsync(alert, token);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Observability exporter failed to handle watchdog alert for user {UserId}.", alert.Payload.UserId);
            return Task.CompletedTask;
        }
    }
}
