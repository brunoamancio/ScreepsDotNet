namespace ScreepsDotNet.Engine.Processors.Steps;

using System.Collections.Generic;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.Helpers;

/// <summary>
/// Resolves simple attack/ranged attack intents by applying flat damage to targets.
/// </summary>
internal sealed class CombatResolutionStep(ICreepDeathProcessor deathProcessor) : IRoomProcessorStep
{
    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        var intents = context.State.Intents;
        if (intents?.Users is null || intents.Users.Count == 0)
            return Task.CompletedTask;

        var hitsUpdates = new Dictionary<string, int>();
        var removals = new HashSet<string>(StringComparer.Ordinal);

        foreach (var envelope in intents.Users.Values)
        {
            if (envelope?.CreepIntents is null || envelope.CreepIntents.Count == 0)
                continue;

            foreach (var (objectId, creepIntent) in envelope.CreepIntents)
            {
                ApplyAttack(creepIntent?.Attack, hitsUpdates, removals);
                ApplyAttack(creepIntent?.RangedAttack, hitsUpdates, removals);
            }
        }

        foreach (var (objectId, hits) in hitsUpdates)
        {
            if (!context.State.Objects.TryGetValue(objectId, out var obj))
                continue;

            var remaining = Math.Max((obj.Hits ?? 0) - hits, 0);
            if (remaining == 0)
            {
                removals.Add(objectId);
                continue;
            }

            context.MutationWriter.Patch(objectId, new RoomObjectPatchPayload
            {
                Hits = remaining
            });
        }

        if (removals.Count > 0)
        {
            var energyLedger = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var id in removals)
            {
                if (!context.State.Objects.TryGetValue(id, out var obj))
                {
                    context.MutationWriter.Remove(id);
                    continue;
                }

                if (obj.IsCreep())
                {
                    deathProcessor.Process(
                        context,
                        obj,
                        new CreepDeathOptions(ViolentDeath: true),
                        energyLedger);
                }
                else
                    context.MutationWriter.Remove(id);
            }
        }

        return Task.CompletedTask;
    }

    private static void ApplyAttack(AttackIntent? intent, IDictionary<string, int> hitsUpdates, ISet<string> removals)
    {
        if (intent is null || string.IsNullOrWhiteSpace(intent.TargetId))
            return;

        var damage = intent.Damage ?? 30;
        hitsUpdates[intent.TargetId!] = hitsUpdates.TryGetValue(intent.TargetId!, out var existing) ? existing + damage : damage;
    }
}
