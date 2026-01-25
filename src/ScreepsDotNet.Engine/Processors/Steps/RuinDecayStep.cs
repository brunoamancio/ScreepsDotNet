namespace ScreepsDotNet.Engine.Processors.Steps;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Processors.Helpers;

/// <summary>
/// Processes ruin decay.
/// Ruins decay after a fixed time period, dropping all contained resources on the ground.
/// </summary>
internal sealed class RuinDecayStep(IResourceDropHelper resourceDropHelper) : IRoomProcessorStep
{
    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        var gameTime = context.State.GameTime;
        var dropContext = resourceDropHelper.CreateContext();

        foreach (var ruin in context.State.Objects.Values) {
            if (!string.Equals(ruin.Type, RoomObjectTypes.Ruin, StringComparison.Ordinal))
                continue;

            ProcessRuin(context, ruin, gameTime, dropContext);
        }

        return Task.CompletedTask;
    }

    private void ProcessRuin(RoomProcessorContext context, RoomObjectSnapshot ruin, int gameTime, ResourceDropContext dropContext)
    {
        // Check if ruin has not expired
        if (ruin.DecayTime.HasValue && gameTime < ruin.DecayTime.Value - 1)
            return;

        // Drop all resources on the ground
        if (ruin.Store.Count > 0) {
            foreach (var (resourceType, amount) in ruin.Store) {
                if (amount > 0) {
                    resourceDropHelper.DropResource(context, ruin, resourceType, amount, dropContext);
                }
            }
        }

        // Remove ruin
        context.MutationWriter.Remove(ruin.Id);
    }
}
