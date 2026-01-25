namespace ScreepsDotNet.Engine.Processors.Steps;

using ScreepsDotNet.Common.Constants;
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
        var healUpdates = new Dictionary<string, int>();
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
                performedAction |= ApplyRangedMassAttack(context, creep, creepIntent?.RangedMassAttack ?? false, hitsUpdates, removals);
                performedAction |= ApplyHeal(context, creepId, creepIntent?.Heal, healUpdates, isRanged: false);
                performedAction |= ApplyHeal(context, creepId, creepIntent?.RangedHeal, healUpdates, isRanged: true);

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

            // Send notification if object has notifyWhenAttacked enabled and took damage
            if (hits > 0 && obj.NotifyWhenAttacked == true && !string.IsNullOrWhiteSpace(obj.UserId)) {
                context.Notifications.SendAttackedNotification(obj.UserId, obj.Id, obj.RoomName);
            }

            if (remaining == 0) {
                removals.Add(objectId);
                continue;  // Node.js destroys without patching when hits <= 0
            }

            context.MutationWriter.Patch(objectId, new RoomObjectPatchPayload
            {
                Hits = remaining
            });
        }

        // Patch targets that were healed
        foreach (var (objectId, healAmount) in healUpdates) {
            if (!context.State.Objects.TryGetValue(objectId, out var obj))
                continue;

            var currentHits = obj.Hits ?? 0;
            var maxHits = obj.HitsMax ?? 0;
            var newHits = Math.Min(currentHits + healAmount, maxHits);

            context.MutationWriter.Patch(objectId, new RoomObjectPatchPayload
            {
                Hits = newHits
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

        // Check for rampart protection (rampart blocks damage to structures/creeps)
        var actualTarget = FindProtectingRampart(context, target, attacker) ?? target;

        // Always track damage (even 0) to match Node.js behavior which patches target even with 0 damage
        hitsUpdates[actualTarget.Id] = hitsUpdates.TryGetValue(actualTarget.Id, out var existing) ? existing + damage : damage;
        return true;
    }

    private static RoomObjectSnapshot? FindProtectingRampart(RoomProcessorContext context, RoomObjectSnapshot target, RoomObjectSnapshot attacker)
    {
        // Only non-rampart structures and creeps can be protected by ramparts
        if (string.Equals(target.Type, RoomObjectTypes.Rampart, StringComparison.Ordinal))
            return null;

        // Look for rampart at target's position
        foreach (var obj in context.State.Objects.Values) {
            if (!string.Equals(obj.Type, RoomObjectTypes.Rampart, StringComparison.Ordinal))
                continue;

            if (obj.X != target.X || obj.Y != target.Y)
                continue;

            // Rampart must belong to same owner as target to protect it
            if (!string.Equals(obj.UserId, target.UserId, StringComparison.Ordinal))
                continue;

            // Rampart protects against attacks from different owners
            if (string.Equals(attacker.UserId, target.UserId, StringComparison.Ordinal))
                continue;

            return obj;
        }

        return null;
    }

    private static bool ApplyRangedMassAttack(RoomProcessorContext context, RoomObjectSnapshot attacker, bool intent, IDictionary<string, int> hitsUpdates, ISet<string> removals)
    {
        if (!intent)
            return false;

        // Calculate base attack power from active RANGED_ATTACK body parts
        var basePower = attacker.Body.Count(p => p.Type == BodyPartType.RangedAttack && p.Hits > 0) * 10;

        if (basePower == 0)
            return false;

        // Distance rate: ranges 0-1 get 100%, range 2 gets 40%, range 3 gets 10%
        var distanceRates = new Dictionary<int, double> { [0] = 1.0, [1] = 1.0, [2] = 0.4, [3] = 0.1 };

        // Find all targets within 3-tile radius (Chebyshev distance)
        foreach (var target in context.State.Objects.Values) {
            // Skip if same user (don't damage own creeps/structures)
            if (string.Equals(target.UserId, attacker.UserId, StringComparison.Ordinal))
                continue;

            // Calculate Chebyshev distance (max of abs x/y differences)
            var distance = Math.Max(Math.Abs(attacker.X - target.X), Math.Abs(attacker.Y - target.Y));

            // Skip if outside 3-tile range
            if (distance > 3)
                continue;

            // Skip if no hits (already destroyed or not damageable)
            if ((target.Hits ?? 0) == 0)
                continue;

            // Skip spawning creeps
            if (target.IsCreep() && target.IsSpawning == true)
                continue;

            // Check for rampart protection (unless target IS the rampart)
            var actualTarget = FindProtectingRampart(context, target, attacker) ?? target;

            // Calculate damage based on distance
            var damageMultiplier = distanceRates.GetValueOrDefault(distance, 0.0);
            var damage = (int)Math.Round(basePower * damageMultiplier);

            // Apply damage (accumulate if target already has pending damage)
            hitsUpdates[actualTarget.Id] = hitsUpdates.TryGetValue(actualTarget.Id, out var existing) ? existing + damage : damage;
        }

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

    private static bool ApplyHeal(RoomProcessorContext context, string healerId, HealIntent? intent, IDictionary<string, int> healUpdates, bool isRanged)
    {
        if (intent is null || string.IsNullOrWhiteSpace(intent.TargetId))
            return false;

        if (!context.State.Objects.TryGetValue(intent.TargetId!, out var target))
            return false;

        // Calculate heal amount based on active heal body parts (only parts with hits > 0 contribute)
        var healAmount = intent.Amount;
        if (!healAmount.HasValue && context.State.Objects.TryGetValue(healerId, out var healer)) {
            healAmount = CalculateHealPower(healer, isRanged);
        }

        // Default to 0 if not calculated
        var finalHealAmount = healAmount ?? 0;

        // Accumulate heal amounts (multiple healers can heal the same target)
        healUpdates[intent.TargetId!] = healUpdates.TryGetValue(intent.TargetId!, out var existing) ? existing + finalHealAmount : finalHealAmount;

        return true;
    }

    private static int CalculateHealPower(RoomObjectSnapshot healer, bool isRanged)
    {
        // Only count heal body parts that have hits > 0 (active parts)
        // This matches Node.js behavior where damaged/destroyed parts don't contribute
        var healPowerPerPart = isRanged ? 4 : 12;  // Ranged heal: 4 HP, Melee heal: 12 HP
        var activeParts = healer.Body.Count(p => p.Type == BodyPartType.Heal && p.Hits > 0);
        return activeParts * healPowerPerPart;
    }
}
