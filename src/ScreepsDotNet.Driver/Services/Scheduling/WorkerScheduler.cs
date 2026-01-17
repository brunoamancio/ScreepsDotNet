namespace ScreepsDotNet.Driver.Services.Scheduling;

using Microsoft.Extensions.Logging;
using ScreepsDotNet.Driver.Abstractions;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Runtime;
using ScreepsDotNet.Driver.Constants;
using ScreepsDotNet.Driver.Services.Runtime;

internal sealed class WorkerScheduler(string name, int concurrency, DriverProcessType? processType = null, IRuntimeTelemetrySink? telemetry = null, ILogger<WorkerScheduler>? logger = null)
{
    private readonly int _concurrency = concurrency > 0
        ? concurrency
        : throw new ArgumentOutOfRangeException(nameof(concurrency));
    private readonly DriverProcessType _processType = processType ?? DriverProcessType.Runtime;
    private readonly IRuntimeTelemetrySink _telemetry = telemetry ?? NullRuntimeTelemetrySink.Instance;

    public Task RunAsync(Func<CancellationToken, Task> worker, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(worker);

        var tasks = new Task[_concurrency];
        for (var i = 0; i < _concurrency; i++) {
            tasks[i] = Task.Run(async () => {
                while (!token.IsCancellationRequested) {
                    try {
                        await worker(token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) {
                        break;
                    }
                    catch (Exception ex) {
                        logger?.LogError(ex, "Worker scheduler '{Name}' worker crashed.", name);
                        await PublishSchedulerFaultAsync(ex, token).ConfigureAwait(false);
                    }
                }
            }, token);
        }

        return Task.WhenAll(tasks);
    }

    private Task PublishSchedulerFaultAsync(Exception exception, CancellationToken token)
    {
        var payload = new RuntimeTelemetryPayload(
            Loop: _processType,
            UserId: $"{name}-worker",
            GameTime: 0,
            CpuLimit: 0,
            CpuBucket: 0,
            CpuUsed: 0,
            TimedOut: false,
            ScriptError: true,
            HeapUsedBytes: 0,
            HeapSizeLimitBytes: 0,
            ErrorMessage: $"scheduler:{exception.GetType().Name}",
            QueueDepth: null,
            ColdStartRequested: false,
            Stage: LoopStageNames.Scheduler.TelemetryStage);
        return _telemetry.PublishTelemetryAsync(payload, token);
    }
}
