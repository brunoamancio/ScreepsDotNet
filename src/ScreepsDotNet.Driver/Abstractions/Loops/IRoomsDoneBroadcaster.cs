namespace ScreepsDotNet.Driver.Abstractions.Loops;

public interface IRoomsDoneBroadcaster
{
    Task PublishAsync(int gameTime, CancellationToken token = default);
}
