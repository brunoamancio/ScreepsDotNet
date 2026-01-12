using Microsoft.Extensions.Logging.Abstractions;
using ScreepsDotNet.Driver.Abstractions.Config;
using ScreepsDotNet.Driver.Abstractions.Eventing;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Notifications;
using ScreepsDotNet.Driver.Services;
using ScreepsDotNet.Driver.Services.Runtime;
using ScreepsDotNet.Driver.Tests.TestSupport;

namespace ScreepsDotNet.Driver.Tests.Runtime;

public sealed class RuntimeTelemetryMonitorTests
{
    [Fact]
    public void TryConsumeColdStartRequest_TriggersAfterThreshold()
    {
        var config = new DriverConfig(new FakeEnvironmentService());
        var notifications = new TestNotificationService();
        using var monitor = new RuntimeTelemetryMonitor(config, notifications, NullLogger<RuntimeTelemetryMonitor>.Instance);

        EmitFailure(config, "user1");
        EmitFailure(config, "user1");
        EmitFailure(config, "user1");

        Assert.True(monitor.TryConsumeColdStartRequest("user1"));
        Assert.False(monitor.TryConsumeColdStartRequest("user1"));
        Assert.Single(notifications.SentNotifications);

        EmitSuccess(config, "user1");
        Assert.False(monitor.TryConsumeColdStartRequest("user1"));
    }

    private static void EmitFailure(IDriverConfig config, string userId)
    {
        var payload = new RuntimeTelemetryPayload(
            userId,
            12345,
            50,
            1000,
            200,
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
            userId,
            12346,
            50,
            1000,
            20,
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
}
