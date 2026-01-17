namespace ScreepsDotNet.Engine.Host;

using ScreepsDotNet.Driver.Abstractions.Engine;
using ScreepsDotNet.Engine.Processors.GlobalSteps;

internal sealed class EngineHost(IGlobalProcessor globalProcessor) : IEngineHost
{
    public Task RunGlobalAsync(int gameTime, CancellationToken token = default)
        => globalProcessor.ExecuteAsync(gameTime, token);
}
