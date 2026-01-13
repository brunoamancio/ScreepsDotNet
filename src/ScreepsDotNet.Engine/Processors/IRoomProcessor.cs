namespace ScreepsDotNet.Engine.Processors;

public interface IRoomProcessor
{
    Task ProcessAsync(string roomName, int gameTime, CancellationToken token = default);
}
