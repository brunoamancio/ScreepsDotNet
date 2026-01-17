using Microsoft.Extensions.Logging.Abstractions;
using ScreepsDotNet.Driver.Abstractions;
using ScreepsDotNet.Driver.Abstractions.Eventing;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Notifications;
using ScreepsDotNet.Driver.Abstractions.Observability;
using ScreepsDotNet.Driver.Abstractions.Runtime;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Driver.Services;
using ScreepsDotNet.Driver.Services.Observability;
using ScreepsDotNet.Driver.Services.Runtime;
using ScreepsDotNet.Driver.Tests.TestSupport;

namespace ScreepsDotNet.Driver.Tests.Observability;

public sealed class RuntimeTelemetryMonitorIntegrationTests
{
    [Fact]
    public async Task WatchdogAlertFlowsToObservabilityExporter()
    {
        var config = new DriverConfig(new FakeEnvironmentService());
        var notifications = new NoopNotificationService();
        var exporter = new RecordingExporter();
        var listener = new ObservabilityTelemetryListener(
            exporter,
            Microsoft.Extensions.Options.Options.Create(new ObservabilityOptions { EnableExporter = true }),
            NullLogger<ObservabilityTelemetryListener>.Instance);
        var pipeline = new RuntimeTelemetryPipeline([listener], NullLogger<RuntimeTelemetryPipeline>.Instance);

        using var monitor = new RuntimeTelemetryMonitor(config, notifications, pipeline, NullLogger<RuntimeTelemetryMonitor>.Instance);

        // trigger failures
        var payload = new RuntimeTelemetryPayload(
            Loop: DriverProcessType.Runner,
            UserId: "user123",
            GameTime: 42,
            CpuLimit: 50,
            CpuBucket: 1000,
            CpuUsed: 200,
            TimedOut: true,
            ScriptError: false,
            HeapUsedBytes: 0,
            HeapSizeLimitBytes: 0,
            ErrorMessage: "timeout",
            QueueDepth: 10,
            ColdStartRequested: false,
            Stage: "execute");

        config.EmitRuntimeTelemetry(new RuntimeTelemetryEventArgs(payload));
        config.EmitRuntimeTelemetry(new RuntimeTelemetryEventArgs(payload));
        config.EmitRuntimeTelemetry(new RuntimeTelemetryEventArgs(payload));

        var alert = await exporter.WaitForAlertAsync();
        Assert.Equal("user123", alert.Payload.UserId);
        Assert.Equal(3, alert.ConsecutiveFailures);
    }

    private sealed class NoopNotificationService : INotificationService
    {
        public Task PublishConsoleMessagesAsync(string userId, ConsoleMessagesPayload payload, CancellationToken token = default) => Task.CompletedTask;
        public Task PublishConsoleErrorAsync(string userId, string errorMessage, CancellationToken token = default) => Task.CompletedTask;
        public Task SendNotificationAsync(string userId, string message, NotificationOptions options, CancellationToken token = default) => Task.CompletedTask;
        public Task NotifyRoomsDoneAsync(int gameTime, CancellationToken token = default) => Task.CompletedTask;
    }

    private sealed class RecordingExporter : IObservabilityExporter
    {
        private readonly TaskCompletionSource<RuntimeWatchdogAlert> _alert = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ExportTelemetryAsync(RuntimeTelemetryPayload payload, CancellationToken token = default) => Task.CompletedTask;

        public Task ExportWatchdogAlertAsync(RuntimeWatchdogAlert alert, CancellationToken token = default)
        {
            _alert.TrySetResult(alert);
            return Task.CompletedTask;
        }

        public Task ExportRoomStatsAsync(RoomStatsUpdate update, CancellationToken token = default) => Task.CompletedTask;

        public Task<RuntimeWatchdogAlert> WaitForAlertAsync() => _alert.Task.WaitAsync(TestContext.Current.CancellationToken);
    }
}
