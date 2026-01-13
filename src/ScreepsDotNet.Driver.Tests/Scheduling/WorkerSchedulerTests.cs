using Microsoft.Extensions.Logging.Abstractions;
using ScreepsDotNet.Driver.Abstractions;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Runtime;
using ScreepsDotNet.Driver.Services.Scheduling;
using ScreepsDotNet.Driver.Constants;

namespace ScreepsDotNet.Driver.Tests.Scheduling;

public sealed class WorkerSchedulerTests
{
    [Fact]
    public async Task RunAsync_EmitsTelemetryWhenWorkerCrashes()
    {
        var sink = new RecordingTelemetrySink();
        var scheduler = new WorkerScheduler("runner", 1, DriverProcessType.Runner, sink, NullLogger<WorkerScheduler>.Instance);

        using var cts = new CancellationTokenSource();
        var runTask = scheduler.RunAsync(token =>
        {
            cts.Cancel();
            throw new InvalidOperationException("boom");
        }, cts.Token);

        await runTask;

        var payload = Assert.Single(sink.Payloads);
        Assert.Equal(DriverProcessType.Runner, payload.Loop);
        Assert.Equal("runner-worker", payload.UserId);
        Assert.Equal(LoopStageNames.Scheduler.TelemetryStage, payload.Stage);
        Assert.Equal("scheduler:InvalidOperationException", payload.ErrorMessage);
    }

    private sealed class RecordingTelemetrySink : IRuntimeTelemetrySink
    {
        public List<RuntimeTelemetryPayload> Payloads { get; } = [];

        public Task PublishTelemetryAsync(RuntimeTelemetryPayload payload, CancellationToken token = default)
        {
            Payloads.Add(payload);
            return Task.CompletedTask;
        }

        public Task PublishWatchdogAlertAsync(RuntimeWatchdogAlert alert, CancellationToken token = default) =>
            Task.CompletedTask;
    }
}
