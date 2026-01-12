using Microsoft.Extensions.Logging;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Runtime;

namespace ScreepsDotNet.Driver.Services.Runtime;

internal sealed class LoggingRuntimeTelemetryListener(ILogger<LoggingRuntimeTelemetryListener>? logger = null) : IRuntimeTelemetryListener
{
    private readonly ILogger<LoggingRuntimeTelemetryListener>? _logger = logger;

    public Task OnTelemetryAsync(RuntimeTelemetryPayload payload, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        _logger?.Log(payload.TimedOut || payload.ScriptError ? LogLevel.Warning : LogLevel.Debug,
            "Runtime telemetry for user {UserId}: cpu {CpuUsed}/{CpuLimit} ms (bucket {CpuBucket}) timedOut={TimedOut} scriptError={ScriptError} heap={HeapUsed}/{HeapLimit} bytes",
            payload.UserId,
            payload.CpuUsed,
            payload.CpuLimit,
            payload.CpuBucket,
            payload.TimedOut,
            payload.ScriptError,
            payload.HeapUsedBytes,
            payload.HeapSizeLimitBytes);

        return Task.CompletedTask;
    }

    public Task OnWatchdogAlertAsync(RuntimeWatchdogAlert alert, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(alert);

        _logger?.LogWarning(
            "Runtime watchdog alert for user {UserId} tick {GameTime}: failures={Failures} timedOut={TimedOut} scriptError={ScriptError} error={Error}",
            alert.Payload.UserId,
            alert.Payload.GameTime,
            alert.ConsecutiveFailures,
            alert.Payload.TimedOut,
            alert.Payload.ScriptError,
            alert.Payload.ErrorMessage ?? string.Empty);

        return Task.CompletedTask;
    }
}
