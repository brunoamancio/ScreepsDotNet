namespace ScreepsDotNet.Engine.Processors.GlobalSteps;

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using ScreepsDotNet.Engine.Data.GlobalState;

internal sealed class EngineGlobalProcessor(
    IGlobalStateProvider globalStateProvider,
    IEnumerable<IGlobalProcessorStep> steps,
    ILogger<EngineGlobalProcessor>? logger = null) : IGlobalProcessor
{
    private readonly IReadOnlyList<IGlobalProcessorStep> _steps = steps as IReadOnlyList<IGlobalProcessorStep> ?? steps.ToArray();

    public async Task ExecuteAsync(int gameTime, CancellationToken token = default)
    {
        var state = await globalStateProvider.GetGlobalStateAsync(gameTime, token).ConfigureAwait(false);
        var context = new GlobalProcessorContext(state);

        if (_steps.Count == 0)
        {
            logger?.LogDebug("EngineGlobalProcessor tick {Tick}: no global steps registered.", gameTime);
            return;
        }

        foreach (var step in _steps)
            await step.ExecuteAsync(context, token).ConfigureAwait(false);

        logger?.LogDebug("EngineGlobalProcessor tick {Tick}: executed {StepCount} steps.", gameTime, _steps.Count);
    }
}
