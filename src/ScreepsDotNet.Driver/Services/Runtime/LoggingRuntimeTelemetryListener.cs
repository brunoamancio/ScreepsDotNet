using Microsoft.Extensions.Logging;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Runtime;

namespace ScreepsDotNet.Driver.Services.Runtime;

internal sealed class LoggingRuntimeTelemetryListener(ILogger<LoggingRuntimeTelemetryListener>? logger = null) : IRuntimeTelemetryListener
{
    public Task OnTelemetryAsync(RuntimeTelemetryPayload payload, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        logger?.Log(payload.TimedOut || payload.ScriptError ? LogLevel.Warning : LogLevel.Debug,
            "Runtime telemetry ({Loop}) for user {UserId}: cpu {CpuUsed}/{CpuLimit} ms (bucket {CpuBucket}) queueDepth={QueueDepth} timedOut={TimedOut} scriptError={ScriptError} coldStart={ColdStart} heap={HeapUsed}/{HeapLimit} bytes",
            payload.Loop,
            payload.UserId,
            payload.CpuUsed,
            payload.CpuLimit,
            payload.CpuBucket,
            payload.QueueDepth,
            payload.TimedOut,
            payload.ScriptError,
            payload.ColdStartRequested,
            payload.HeapUsedBytes,
            payload.HeapSizeLimitBytes);

        return Task.CompletedTask;
    }

    public Task OnWatchdogAlertAsync(RuntimeWatchdogAlert alert, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(alert);

        logger?.LogWarning(
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
