using ScreepsDotNet.Driver.Abstractions.Notifications;
using ScreepsDotNet.Driver.Contracts;

namespace ScreepsDotNet.Driver.Abstractions.Loops;

public interface IDriverLoopHooks
{
    Task SaveRoomHistoryAsync(string roomName, int gameTime, RoomHistoryTickPayload payload, CancellationToken token = default);
    Task UploadRoomHistoryChunkAsync(string roomName, int baseGameTime, CancellationToken token = default);

    Task PublishConsoleMessagesAsync(string userId, ConsoleMessagesPayload payload, CancellationToken token = default);
    Task PublishConsoleErrorAsync(string userId, string errorMessage, CancellationToken token = default);
    Task SendNotificationAsync(string userId, string message, NotificationOptions options, CancellationToken token = default);
    Task NotifyRoomsDoneAsync(int gameTime, CancellationToken token = default);
    Task PublishRuntimeTelemetryAsync(RuntimeTelemetryPayload payload, CancellationToken token = default);
}
