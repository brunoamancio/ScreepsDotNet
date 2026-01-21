namespace ScreepsDotNet.Engine.Host;

using ScreepsDotNet.Driver.Abstractions.Engine;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.GlobalSteps;

internal sealed class EngineHost(IGlobalProcessor globalProcessor, IRoomProcessor roomProcessor) : IEngineHost
{
    public Task RunGlobalAsync(int gameTime, CancellationToken token = default)
        => globalProcessor.ExecuteAsync(gameTime, token);

    public Task RunRoomAsync(string roomName, int gameTime, CancellationToken token = default)
        => roomProcessor.ProcessAsync(roomName, gameTime, token);
}
