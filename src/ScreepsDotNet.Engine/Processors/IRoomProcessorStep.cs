namespace ScreepsDotNet.Engine.Processors;

public interface IRoomProcessorStep
{
    Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default);
}
