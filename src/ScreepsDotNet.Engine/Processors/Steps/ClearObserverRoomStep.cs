namespace ScreepsDotNet.Engine.Processors.Steps;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Processors;

/// <summary>
/// Clears the observeRoom field for all observers at the start of the tick.
/// The observeRoom property is temporary and only lasts for one tick.
/// </summary>
internal sealed class ClearObserverRoomStep : IRoomProcessorStep
{
    /// <summary>
    /// Clears observeRoom for all observers in the room.
    /// </summary>
    /// <param name="context">The room processor context containing state and mutation writers.</param>
    /// <param name="token">Cancellation token for async operations.</param>
    /// <returns>A completed task.</returns>
    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        foreach (var obj in context.State.Objects.Values) {
            if (!string.Equals(obj.Type, RoomObjectTypes.Observer, StringComparison.Ordinal) || obj.ObserveRoom is null) continue;

            // Clear observeRoom via global mutation
            var patch = new GlobalRoomObjectPatch(ClearObserveRoom: true);
            context.GlobalMutationWriter.PatchRoomObject(obj.Id, patch);
        }

        return Task.CompletedTask;
    }
}
