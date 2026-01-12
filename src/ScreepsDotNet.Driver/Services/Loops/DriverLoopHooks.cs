using Microsoft.Extensions.Logging;
using ScreepsDotNet.Driver.Abstractions.History;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Notifications;

namespace ScreepsDotNet.Driver.Services.Loops;

internal sealed class DriverLoopHooks(
    IHistoryService historyService,
    INotificationService notificationService,
    ILogger<DriverLoopHooks>? logger = null) : IDriverLoopHooks
{
    private readonly IHistoryService _history = historyService;
    private readonly INotificationService _notifications = notificationService;
    private readonly ILogger<DriverLoopHooks>? _logger = logger;

    public Task SaveRoomHistoryAsync(string roomName, int gameTime, string serializedObjects, CancellationToken token = default)
        => _history.SaveRoomHistoryAsync(roomName, gameTime, serializedObjects, token);

    public Task UploadRoomHistoryChunkAsync(string roomName, int baseGameTime, CancellationToken token = default)
        => _history.UploadRoomHistoryChunkAsync(roomName, baseGameTime, token);

    public Task PublishConsoleMessagesAsync(string userId, ConsoleMessagesPayload payload, CancellationToken token = default)
        => _notifications.PublishConsoleMessagesAsync(userId, payload, token);

    public Task PublishConsoleErrorAsync(string userId, string errorMessage, CancellationToken token = default)
        => _notifications.PublishConsoleErrorAsync(userId, errorMessage, token);

    public Task SendNotificationAsync(string userId, string message, NotificationOptions options, CancellationToken token = default)
        => _notifications.SendNotificationAsync(userId, message, options, token);

    public Task NotifyRoomsDoneAsync(int gameTime, CancellationToken token = default)
        => _notifications.NotifyRoomsDoneAsync(gameTime, token);

    public Task PublishRuntimeTelemetryAsync(RuntimeTelemetryPayload payload, CancellationToken token = default)
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
}
