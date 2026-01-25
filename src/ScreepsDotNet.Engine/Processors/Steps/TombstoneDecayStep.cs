namespace ScreepsDotNet.Engine.Processors.Steps;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Processors.Helpers;

/// <summary>
/// Processes tombstone decay.
/// Tombstones decay after a fixed time period, dropping all contained resources on the ground.
/// </summary>
internal sealed class TombstoneDecayStep(IResourceDropHelper resourceDropHelper) : IRoomProcessorStep
{
    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        var gameTime = context.State.GameTime;
        var dropContext = resourceDropHelper.CreateContext();

        foreach (var tombstone in context.State.Objects.Values) {
            if (!string.Equals(tombstone.Type, RoomObjectTypes.Tombstone, StringComparison.Ordinal))
                continue;

            ProcessTombstone(context, tombstone, gameTime, dropContext);
        }

        return Task.CompletedTask;
    }

    private void ProcessTombstone(RoomProcessorContext context, RoomObjectSnapshot tombstone, int gameTime, ResourceDropContext dropContext)
    {
        // Check if tombstone has not expired
        if (tombstone.DecayTime.HasValue && gameTime < tombstone.DecayTime.Value - 1)
            return;

        // Drop all resources on the ground
        if (tombstone.Store.Count > 0) {
            foreach (var (resourceType, amount) in tombstone.Store) {
                if (amount > 0) {
                    resourceDropHelper.DropResource(context, tombstone, resourceType, amount, dropContext);
                }
            }
        }

        // Remove tombstone
        context.MutationWriter.Remove(tombstone.Id);
    }
}
