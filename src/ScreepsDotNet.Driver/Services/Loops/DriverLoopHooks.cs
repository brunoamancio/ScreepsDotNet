using Microsoft.Extensions.Logging;
using ScreepsDotNet.Driver.Abstractions.History;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Notifications;
using ScreepsDotNet.Driver.Abstractions.Runtime;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Driver.Services.Runtime;

namespace ScreepsDotNet.Driver.Services.Loops;

internal sealed class DriverLoopHooks(IHistoryService historyService, INotificationService notificationService, IRuntimeTelemetrySink telemetrySink,
                                      IRoomsDoneBroadcaster roomsDoneBroadcaster, ILogger<DriverLoopHooks>? logger = null)
    : IDriverLoopHooks
{
    private readonly ILogger<DriverLoopHooks>? _logger = logger;
    private readonly IRuntimeTelemetrySink _telemetrySink = telemetrySink ?? NullRuntimeTelemetrySink.Instance;

    public Task SaveRoomHistoryAsync(string roomName, int gameTime, RoomHistoryTickPayload payload, CancellationToken token = default)
        => historyService.SaveRoomHistoryAsync(roomName, gameTime, payload, token);

    public Task UploadRoomHistoryChunkAsync(string roomName, int baseGameTime, CancellationToken token = default)
        => historyService.UploadRoomHistoryChunkAsync(roomName, baseGameTime, token);

    public Task PublishConsoleMessagesAsync(string userId, ConsoleMessagesPayload payload, CancellationToken token = default)
        => notificationService.PublishConsoleMessagesAsync(userId, payload, token);

    public Task PublishConsoleErrorAsync(string userId, string errorMessage, CancellationToken token = default)
        => notificationService.PublishConsoleErrorAsync(userId, errorMessage, token);

    public Task SendNotificationAsync(string userId, string message, NotificationOptions options, CancellationToken token = default)
        => notificationService.SendNotificationAsync(userId, message, options, token);

    public Task NotifyRoomsDoneAsync(int gameTime, CancellationToken token = default)
    {
        _ = roomsDoneBroadcaster.PublishAsync(gameTime, token);
        return notificationService.NotifyRoomsDoneAsync(gameTime, token);
    }

    public Task PublishRuntimeTelemetryAsync(RuntimeTelemetryPayload payload, CancellationToken token = default) =>
        _telemetrySink.PublishTelemetryAsync(payload, token);
}
