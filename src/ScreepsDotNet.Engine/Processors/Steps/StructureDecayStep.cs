using ScreepsDotNet.Common.Constants;

namespace ScreepsDotNet.Engine.Processors.Steps;

using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Processors;

/// <summary>
/// Applies passive decay to walls/ramparts/roads based on decay intervals.
/// Ramparts: Lose 300 hits every 100 ticks.
/// Walls: Lose 1 hit per tick (no interval).
/// Roads: Lose 100 hits every 1000 ticks.
/// </summary>
internal sealed class StructureDecayStep : IRoomProcessorStep
{
    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        var gameTime = context.State.GameTime;

        foreach (var structure in context.State.Objects.Values) {
            // Skip if already removed by another step
            if (context.MutationWriter.IsMarkedForRemoval(structure.Id))
                continue;

            if (structure.Type is not (RoomObjectTypes.Wall or RoomObjectTypes.Rampart or RoomObjectTypes.Road))
                continue;

            if (structure.Hits is not > 0)
                continue;

            // Get decay configuration for this structure type
            var (decayAmount, decayInterval, requiresDecayTime) = structure.Type switch
            {
                RoomObjectTypes.Rampart => (ScreepsGameConstants.RampartDecayAmount, ScreepsGameConstants.RampartDecayInterval, true),
                RoomObjectTypes.Road => (ScreepsGameConstants.RoadDecayAmount, ScreepsGameConstants.RoadDecayInterval, true),
                RoomObjectTypes.Wall => (1, 1, false), // Walls decay 1 hit per tick (no interval)
                _ => (0, 1, false)
            };

            // Structures without intervals (walls) decay every tick
            if (!requiresDecayTime) {
                var nextHits = Math.Max(structure.Hits.Value - decayAmount, 0);
                if (nextHits == structure.Hits)
                    continue;

                context.MutationWriter.Patch(structure.Id, new RoomObjectPatchPayload
                {
                    Hits = nextHits
                });

                continue;
            }

            // Structures with intervals (ramparts, roads) use DecayTime property
            // Check if DecayTime is missing or if it's time to decay
            // NOTE: Node.js uses `gameTime >= nextDecayTime-1`, causing decay 1 tick early (parity quirk)
            var shouldDecay = !structure.DecayTime.HasValue || gameTime >= structure.DecayTime.Value - 1;
            if (!shouldDecay)
                continue;

            // Get current hits value, accounting for pending patches from earlier steps (e.g., CombatResolutionStep)
            // Node.js modifies hits in-place, so later processors see modified values. We emulate this by checking pending patches.
            var currentHits = context.MutationWriter.TryGetPendingPatch(structure.Id, out var pendingPatch) && pendingPatch.Hits.HasValue
                ? pendingPatch.Hits.Value
                : structure.Hits.Value;

            // Apply decay
            var newHits = Math.Max(currentHits - decayAmount, 0);
            var nextDecayTime = gameTime + decayInterval;

            // If structure is destroyed, don't set nextDecayTime (will be removed)
            if (newHits == 0) {
                context.MutationWriter.Patch(structure.Id, new RoomObjectPatchPayload
                {
                    Hits = 0
                });
                context.MutationWriter.Remove(structure.Id);
            }
            else {
                context.MutationWriter.Patch(structure.Id, new RoomObjectPatchPayload
                {
                    Hits = newHits,
                    DecayTime = nextDecayTime
                });
            }
        }

        return Task.CompletedTask;
    }
}
