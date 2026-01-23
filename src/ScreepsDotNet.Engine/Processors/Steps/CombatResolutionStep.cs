namespace ScreepsDotNet.Engine.Processors.Steps;

using ScreepsDotNet.Common.Types;
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
        var creepActors = new HashSet<string>(StringComparer.Ordinal);

        foreach (var envelope in intents.Users.Values) {
            if (envelope?.CreepIntents is null || envelope.CreepIntents.Count == 0)
                continue;
            foreach (var (creepId, creepIntent) in envelope.CreepIntents) {
                if (!context.State.Objects.TryGetValue(creepId, out var creep))
                    continue;

                var performedAction = false;
                performedAction |= ApplyAttack(context, creep, creepIntent?.Attack, hitsUpdates, removals, isRanged: false);
                performedAction |= ApplyAttack(context, creep, creepIntent?.RangedAttack, hitsUpdates, removals, isRanged: true);
                performedAction |= ApplyHeal(context, creepId, creepIntent?.Heal);

                if (performedAction)
                    creepActors.Add(creepId);
            }
        }

        // Patch creeps that performed actions (attacker/healer)
        foreach (var creepId in creepActors) {
            if (!context.State.Objects.ContainsKey(creepId))
                continue;

            // Create empty patch to signal this creep acted
            context.MutationWriter.Patch(creepId, new RoomObjectPatchPayload());
        }

        // Patch targets that took damage
        foreach (var (objectId, hits) in hitsUpdates) {
            if (!context.State.Objects.TryGetValue(objectId, out var obj))
                continue;

            var remaining = Math.Max((obj.Hits ?? 0) - hits, 0);
            if (remaining == 0) {
                removals.Add(objectId);
                continue;
            }

            context.MutationWriter.Patch(objectId, new RoomObjectPatchPayload
            {
                Hits = remaining
            });
        }

        if (removals.Count > 0) {
            var energyLedger = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var id in removals) {
                if (!context.State.Objects.TryGetValue(id, out var obj)) {
                    context.MutationWriter.Remove(id);
                    continue;
                }

                if (obj.IsCreep()) {
                    deathProcessor.Process(
                        context,
                        obj,
                        new CreepDeathOptions(ViolentDeath: true),
                        energyLedger);
                }
                else {
                    context.MutationWriter.Remove(id);
                }
            }
        }

        return Task.CompletedTask;
    }

    private static bool ApplyAttack(RoomProcessorContext context, RoomObjectSnapshot attacker, AttackIntent? intent, IDictionary<string, int> hitsUpdates, ISet<string> removals, bool isRanged)
    {
        if (intent is null || string.IsNullOrWhiteSpace(intent.TargetId))
            return false;

        // Get target object
        if (!context.State.Objects.TryGetValue(intent.TargetId!, out var target))
            return false;

        // Calculate damage based on active attack body parts (only parts with hits > 0 contribute)
        var damage = intent.Damage ?? CalculateAttackPower(attacker, isRanged);

        // Always track damage (even 0) to match Node.js behavior which patches target even with 0 damage
        hitsUpdates[intent.TargetId!] = hitsUpdates.TryGetValue(intent.TargetId!, out var existing) ? existing + damage : damage;
        return true;
    }

    private static int CalculateAttackPower(RoomObjectSnapshot attacker, bool isRanged)
    {
        // Only count attack body parts that have hits > 0 (active parts)
        // This matches Node.js behavior where damaged/destroyed parts don't contribute
        var bodyPartType = isRanged ? BodyPartType.RangedAttack : BodyPartType.Attack;
        var powerPerPart = isRanged ? 10 : 30;
        var result = attacker.Body.Count(p => p.Type == bodyPartType && p.Hits > 0) * powerPerPart;
        return result;
    }

    private static bool ApplyHeal(RoomProcessorContext context, string healerId, HealIntent? intent)
    {
        if (intent is null || string.IsNullOrWhiteSpace(intent.TargetId))
            return false;

        if (!context.State.Objects.TryGetValue(intent.TargetId!, out var target))
            return false;

        // Calculate heal amount based on active heal body parts (only parts with hits > 0 contribute)
        var healAmount = intent.Amount;
        if (!healAmount.HasValue && context.State.Objects.TryGetValue(healerId, out var healer)) {
            healAmount = CalculateHealPower(healer);
        }

        // Default to 0 if not calculated
        var finalHealAmount = healAmount ?? 0;

        var currentHits = target.Hits ?? 0;
        var maxHits = target.HitsMax ?? 0;
        var newHits = Math.Min(currentHits + finalHealAmount, maxHits);

        // Always patch target to match Node.js behavior (even if healAmount is 0 or hits don't change)
        // Node.js sets target._healToApply regardless of heal power
        context.MutationWriter.Patch(intent.TargetId!, new RoomObjectPatchPayload
        {
            Hits = newHits
        });

        return true;
    }

    private static int CalculateHealPower(RoomObjectSnapshot healer)
    {
        // Only count heal body parts that have hits > 0 (active parts)
        // This matches Node.js behavior where damaged/destroyed parts don't contribute
        const int healPowerPerPart = 12;
        var activeParts = healer.Body.Count(p => p.Type == BodyPartType.Heal && p.Hits > 0);
        return activeParts * healPowerPerPart;
    }
}
