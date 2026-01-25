namespace ScreepsDotNet.Engine.Processors.Helpers;

using ScreepsDotNet.Driver.Abstractions.Notifications;
using ScreepsDotNet.Driver.Constants;

internal sealed class NullNotificationSink : INotificationSink
{
    public void SendAttackedNotification(string userId, string objectId, string roomName) { }
    public Task FlushAsync(CancellationToken token = default) => Task.CompletedTask;
}

internal sealed class RoomNotificationSink(INotificationService notificationService, string roomName) : INotificationSink
{
    private readonly Dictionary<string, HashSet<string>> _attackNotifications = new(StringComparer.Ordinal);

    public void SendAttackedNotification(string userId, string objectId, string roomName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);

        if (!_attackNotifications.TryGetValue(userId, out var objects)) {
            objects = new HashSet<string>(StringComparer.Ordinal);
            _attackNotifications[userId] = objects;
        }

        objects.Add(objectId);
    }

    public async Task FlushAsync(CancellationToken token = default)
    {
        foreach (var (userId, objects) in _attackNotifications) {
            var message = objects.Count == 1
                ? $"Your structures/creeps in room {roomName} are under attack!"
                : $"{objects.Count} of your structures/creeps in room {roomName} are under attack!";

            var options = new NotificationOptions(
                GroupIntervalMinutes: 5,
                Type: NotificationTypes.Attack
            );

            await notificationService.SendNotificationAsync(userId, message, options, token).ConfigureAwait(false);
        }

        _attackNotifications.Clear();
    }
}
