namespace ScreepsDotNet.Engine.Processors;

using Microsoft.Extensions.Logging;
using System.Linq;
using ScreepsDotNet.Engine.Data.Bulk;
using ScreepsDotNet.Engine.Data.Rooms;
using ScreepsDotNet.Engine.Data.Memory;

internal sealed class RoomProcessor(
    IRoomStateProvider roomStateProvider,
    IRoomMutationWriterFactory mutationWriterFactory,
    IUserMemorySink memorySink,
    IEnumerable<IRoomProcessorStep> steps,
    ILogger<RoomProcessor>? logger = null) : IRoomProcessor
{
    public async Task ProcessAsync(string roomName, int gameTime, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);

        var state = await roomStateProvider.GetRoomStateAsync(roomName, gameTime, token).ConfigureAwait(false);
        var writer = mutationWriterFactory.Create(roomName);
        var context = new RoomProcessorContext(state, writer);

        try
        {
            foreach (var step in steps)
                await step.ExecuteAsync(context, token).ConfigureAwait(false);

            // Placeholder logging until real simulation steps populate mutations/memory.
            if (!steps.Any())
                logger?.LogDebug("RoomProcessor tick {Tick} room {Room} has {ObjectCount} objects.", state.GameTime, state.RoomName, state.Objects.Count);

            await writer.FlushAsync(token).ConfigureAwait(false);

            await context.FlushMemoryAsync(memorySink, token).ConfigureAwait(false);
        }
        finally
        {
            writer.Reset();
            context.ClearPendingMemory();
        }
    }
}
