namespace ScreepsDotNet.Engine.Processors.GlobalSteps;

using Microsoft.Extensions.Logging;
using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Engine.Data.GlobalState;

/// <summary>
/// Consumes pending inter-room transfers so edge/portal moves complete inside the managed engine.
/// </summary>
internal sealed class InterRoomTransferStep(IInterRoomTransferProcessor transferProcessor, IGlobalStateProvider globalStateProvider, ILogger<InterRoomTransferStep>? logger = null) : IGlobalProcessorStep
{
    public async Task ExecuteAsync(GlobalProcessorContext context, CancellationToken token = default)
    {
        var moved = await transferProcessor.ProcessTransfersAsync(context.State.AccessibleRooms, token)
                                           .ConfigureAwait(false);

        if (moved <= 0)
            return;

        logger?.LogDebug("InterRoomTransferStep tick {Tick}: moved {Count} creeps.", context.GameTime, moved);
        globalStateProvider.Invalidate();
    }
}
