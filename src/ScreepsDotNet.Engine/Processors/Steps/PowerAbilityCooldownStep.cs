using ScreepsDotNet.Common.Constants;

namespace ScreepsDotNet.Engine.Processors.Steps;

using System;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Processors;

/// <summary>
/// Applies a simple cooldown for power creep abilities and reduces spawn cooldown timers.
/// </summary>
internal sealed class PowerAbilityCooldownStep : IRoomProcessorStep
{
    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        foreach (var obj in context.State.Objects.Values)
        {
            if (obj.Type != RoomObjectTypes.PowerCreep)
                continue;

            int? structureHits = null;
            if (obj.Structure is not null && obj.Structure.Hits is > 0)
            {
                var reduced = Math.Max((obj.Structure.Hits ?? 0) - 10, 0);
                if (reduced != obj.Structure.Hits)
                    structureHits = reduced;
            }

            int? spawnCooldownTime = null;
            if (obj.SpawnCooldownTime is > 0)
                spawnCooldownTime = Math.Max(obj.SpawnCooldownTime.Value - 1, 0);

            if (structureHits is null && spawnCooldownTime is null)
                continue;

            var patch = new RoomObjectPatchPayload
            {
                StructureHits = structureHits,
                SpawnCooldownTime = spawnCooldownTime
            };

            context.MutationWriter.Patch(obj.Id, patch);
        }

        return Task.CompletedTask;
    }
}
