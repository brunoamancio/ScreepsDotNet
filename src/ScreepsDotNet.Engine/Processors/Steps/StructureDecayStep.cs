using ScreepsDotNet.Common.Constants;

namespace ScreepsDotNet.Engine.Processors.Steps;

using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Processors;

/// <summary>
/// Applies passive decay to walls/ramparts/roads to mimic legacy structure upkeep.
/// </summary>
internal sealed class StructureDecayStep : IRoomProcessorStep
{
    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        foreach (var structure in context.State.Objects.Values) {
            if (structure.Type is not (RoomObjectTypes.Wall or RoomObjectTypes.Rampart or RoomObjectTypes.Road))
                continue;

            if (structure.Hits is not > 0)
                continue;

            var decay = structure.Type switch
            {
                RoomObjectTypes.Wall => 1,
                RoomObjectTypes.Rampart => 100,
                _ => 50
            };

            var nextHits = Math.Max(structure.Hits.Value - decay, 0);
            if (nextHits == structure.Hits)
                continue;

            var patch = new RoomObjectPatchPayload
            {
                Hits = nextHits
            };

            context.MutationWriter.Patch(structure.Id, patch);
        }

        return Task.CompletedTask;
    }
}
