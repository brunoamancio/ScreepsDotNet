namespace ScreepsDotNet.Engine.Processors.Steps;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Contracts;

/// <summary>
/// Processes energy (resource) decay on the ground.
/// Dropped resources gradually decay over time based on the ENERGY_DECAY constant.
/// </summary>
internal sealed class EnergyDecayStep : IRoomProcessorStep
{
    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        foreach (var resource in context.State.Objects.Values) {
            if (!string.Equals(resource.Type, RoomObjectTypes.Resource, StringComparison.Ordinal))
                continue;

            ProcessResource(context, resource);
        }

        return Task.CompletedTask;
    }

    private static void ProcessResource(RoomProcessorContext context, RoomObjectSnapshot resource)
    {
        // Skip if already removed by another step (e.g., pickup)
        if (context.MutationWriter.IsMarkedForRemoval(resource.Id))
            return;

        var amount = resource.Energy ?? 0;

        if (amount <= 0) {
            context.MutationWriter.Remove(resource.Id);
            return;
        }

        // Calculate decay: amount decreases by ceil(amount / ENERGY_DECAY) each tick
        var decayAmount = (int)Math.Ceiling((double)amount / ScreepsGameConstants.EnergyDecay);
        var newAmount = amount - decayAmount;

        if (newAmount <= 0) {
            // Resource completely decayed, remove it
            context.MutationWriter.Remove(resource.Id);
        }
        else {
            // Update resource amount
            context.MutationWriter.Patch(resource.Id, new RoomObjectPatchPayload
            {
                Energy = newAmount
            });
        }
    }
}
