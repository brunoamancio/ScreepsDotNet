namespace ScreepsDotNet.Engine.Processors.Helpers;

/// <summary>
/// Sink for attack notifications. Batches notifications per room tick and delegates to Driver's INotificationService.
/// </summary>
public interface INotificationSink
{
    void SendAttackedNotification(string userId, string objectId, string roomName);
    Task FlushAsync(CancellationToken token = default);
}
