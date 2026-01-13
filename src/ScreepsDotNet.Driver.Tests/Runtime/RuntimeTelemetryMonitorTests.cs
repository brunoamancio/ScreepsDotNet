using Microsoft.Extensions.Logging.Abstractions;
using ScreepsDotNet.Driver.Abstractions;
using ScreepsDotNet.Driver.Abstractions.Config;
using ScreepsDotNet.Driver.Abstractions.Eventing;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Notifications;
using ScreepsDotNet.Driver.Abstractions.Runtime;
using ScreepsDotNet.Driver.Services;
using ScreepsDotNet.Driver.Services.Runtime;
using ScreepsDotNet.Driver.Tests.TestSupport;

namespace ScreepsDotNet.Driver.Tests.Runtime;

public sealed class RuntimeTelemetryMonitorTests
{
    [Fact]
    public async Task TryConsumeColdStartRequest_TriggersAfterThreshold()
    {
        var config = new DriverConfig(new FakeEnvironmentService());
        var notifications = new TestNotificationService();
        var sink = new TestTelemetrySink();
        using var monitor = new RuntimeTelemetryMonitor(config, notifications, sink, NullLogger<RuntimeTelemetryMonitor>.Instance);

        EmitFailure(config, "user1");
        EmitFailure(config, "user1");
        EmitFailure(config, "user1");

        await sink.WaitForAlertAsync(TimeSpan.FromSeconds(5));
        Assert.True(monitor.TryConsumeColdStartRequest("user1"));
        Assert.False(monitor.TryConsumeColdStartRequest("user1"));
        Assert.Single(notifications.SentNotifications);
        Assert.Single(sink.Alerts);

        EmitSuccess(config, "user1");
        Assert.False(monitor.TryConsumeColdStartRequest("user1"));
    }

    private static void EmitFailure(IDriverConfig config, string userId)
    {
        var payload = new RuntimeTelemetryPayload(
            Loop: DriverProcessType.Runner,
            UserId: userId,
            GameTime: 12345,
            CpuLimit: 50,
            CpuBucket: 1000,
            CpuUsed: 200,
            TimedOut: true,
            ScriptError: false,
            HeapUsedBytes: 0,
            HeapSizeLimitBytes: 0,
            ErrorMessage: "timeout");
        config.EmitRuntimeTelemetry(new RuntimeTelemetryEventArgs(payload));
    }

    private static void EmitSuccess(IDriverConfig config, string userId)
    {
        var payload = new RuntimeTelemetryPayload(
            Loop: DriverProcessType.Runner,
            UserId: userId,
            GameTime: 12346,
            CpuLimit: 50,
            CpuBucket: 1000,
            CpuUsed: 20,
            TimedOut: false,
            ScriptError: false,
            HeapUsedBytes: 0,
            HeapSizeLimitBytes: 0,
            ErrorMessage: null);
        config.EmitRuntimeTelemetry(new RuntimeTelemetryEventArgs(payload));
    }

    private sealed class TestNotificationService : INotificationService
    {
        public List<(string UserId, string Message, NotificationOptions Options)> SentNotifications { get; } = [];

        public Task PublishConsoleMessagesAsync(string userId, ConsoleMessagesPayload payload, CancellationToken token = default) =>
            Task.CompletedTask;

        public Task PublishConsoleErrorAsync(string userId, string errorMessage, CancellationToken token = default) =>
            Task.CompletedTask;

        public Task SendNotificationAsync(string userId, string message, NotificationOptions options, CancellationToken token = default)
        {
            SentNotifications.Add((userId, message, options));
            return Task.CompletedTask;
        }

        public Task NotifyRoomsDoneAsync(int gameTime, CancellationToken token = default) =>
            Task.CompletedTask;
    }

    private sealed class TestTelemetrySink : IRuntimeTelemetrySink
    {
        private readonly TaskCompletionSource<RuntimeWatchdogAlert> _alertSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<RuntimeWatchdogAlert> Alerts { get; } = [];

        public Task PublishTelemetryAsync(RuntimeTelemetryPayload payload, CancellationToken token = default)
            => Task.CompletedTask;

        public Task PublishWatchdogAlertAsync(RuntimeWatchdogAlert alert, CancellationToken token = default)
        {
            Alerts.Add(alert);
            _alertSource.TrySetResult(alert);
            return Task.CompletedTask;
        }

        public Task WaitForAlertAsync(TimeSpan? timeout = null)
        {
            if (timeout is { } window)
                return _alertSource.Task.WaitAsync(window);
            return _alertSource.Task;
        }
    }
}
