namespace ScreepsDotNet.Driver.Abstractions.Notifications;

public interface INotificationService
{
    Task PublishConsoleMessagesAsync(string userId, ConsoleMessagesPayload payload, CancellationToken token = default);
    Task PublishConsoleErrorAsync(string userId, string errorMessage, CancellationToken token = default);
    Task SendNotificationAsync(string userId, string message, NotificationOptions options, CancellationToken token = default);
    Task NotifyRoomsDoneAsync(int gameTime, CancellationToken token = default);
}

public sealed record ConsoleMessagesPayload(IReadOnlyList<string> Log, IReadOnlyList<string> Results);

public sealed record NotificationOptions(int GroupIntervalMinutes = 0, string Type = "msg");
