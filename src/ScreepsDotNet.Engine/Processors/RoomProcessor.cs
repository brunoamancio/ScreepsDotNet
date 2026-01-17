namespace ScreepsDotNet.Engine.Processors;

using System.Linq;
using Microsoft.Extensions.Logging;
using ScreepsDotNet.Driver.Abstractions.History;
using ScreepsDotNet.Engine.Data.Bulk;
using ScreepsDotNet.Engine.Data.GlobalState;
using ScreepsDotNet.Engine.Data.Memory;
using ScreepsDotNet.Engine.Data.Rooms;
using ScreepsDotNet.Engine.Processors.Helpers;

internal sealed class RoomProcessor(
    IRoomStateProvider roomStateProvider,
    IGlobalStateProvider globalStateProvider,
    IRoomMutationWriterFactory mutationWriterFactory,
    IUserMemorySink memorySink,
    IHistoryService historyService,
    IEnumerable<IRoomProcessorStep> steps,
    ILogger<RoomProcessor>? logger = null) : IRoomProcessor
{
    public async Task ProcessAsync(string roomName, int gameTime, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);

        var state = await roomStateProvider.GetRoomStateAsync(roomName, gameTime, token).ConfigureAwait(false);
        var globalState = await globalStateProvider.GetGlobalStateAsync(state.GameTime, token).ConfigureAwait(false);
        var writer = mutationWriterFactory.Create(roomName);
        var statsSink = new RoomStatsSink(historyService.CreateRoomStatsUpdater(roomName));
        globalState.ExitTopology.TryGetValue(roomName, out var exitTopology);
        var context = new RoomProcessorContext(state, writer, statsSink, exitTopology);

        try {
            foreach (var step in steps)
                await step.ExecuteAsync(context, token).ConfigureAwait(false);

            // Placeholder logging until real simulation steps populate mutations/memory.
            if (!steps.Any())
                logger?.LogDebug("RoomProcessor tick {Tick} room {Room} has {ObjectCount} objects.", state.GameTime, state.RoomName, state.Objects.Count);

            await writer.FlushAsync(token).ConfigureAwait(false);

            await context.Stats.FlushAsync(state.GameTime, token).ConfigureAwait(false);

            await context.FlushMemoryAsync(memorySink, token).ConfigureAwait(false);
        }
        finally {
            writer.Reset();
            context.ClearPendingMemory();
        }
    }
}
