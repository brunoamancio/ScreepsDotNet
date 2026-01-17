using Microsoft.Extensions.Logging;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Observability;
using ScreepsDotNet.Driver.Abstractions.Runtime;
using ScreepsDotNet.Driver.Contracts;

namespace ScreepsDotNet.Driver.Services.Observability;

internal sealed class LoggingObservabilityExporter(ILogger<LoggingObservabilityExporter>? logger = null) : IObservabilityExporter
{
    public Task ExportTelemetryAsync(RuntimeTelemetryPayload payload, CancellationToken token = default)
    {
        logger?.LogInformation(
            "[Telemetry] loop={Loop} stage={Stage} user={User} cpu={CpuUsed}/{CpuLimit} bucket={Bucket} queue={QueueDepth} timeout={Timeout} scriptError={ScriptError}",
            payload.Loop,
            payload.Stage ?? "n/a",
            payload.UserId,
            payload.CpuUsed,
            payload.CpuLimit,
            payload.CpuBucket,
            payload.QueueDepth,
            payload.TimedOut,
            payload.ScriptError);
        return Task.CompletedTask;
    }

    public Task ExportWatchdogAlertAsync(RuntimeWatchdogAlert alert, CancellationToken token = default)
    {
        logger?.LogWarning(
            "[Watchdog] user={User} failures={Failures} stage={Stage} timeout={Timeout} scriptError={ScriptError}",
            alert.Payload.UserId,
            alert.ConsecutiveFailures,
            alert.Payload.Stage ?? "n/a",
            alert.Payload.TimedOut,
            alert.Payload.ScriptError);
        return Task.CompletedTask;
    }

    public Task ExportRoomStatsAsync(RoomStatsUpdate update, CancellationToken token = default)
    {
        logger?.LogInformation(
            "[RoomStats] room={Room} tick={Tick} users={UserCount}",
            update.Room,
            update.GameTime,
            update.Metrics.Count);
        return Task.CompletedTask;
    }
}
