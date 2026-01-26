using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Extensions;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Processors.Helpers;

namespace ScreepsDotNet.Engine.Processors.Steps;

/// <summary>
/// Implements invader AI: healer mode, attacker mode, flee behavior, and ranged attacks.
/// Invaders attack player creeps/structures and coordinate with other invaders.
/// </summary>
internal sealed class InvaderAiStep : IRoomProcessorStep
{
    private const int FleeRangeHealers = 4; // Range healers flee from hostiles
    private const int FleeRangeRanged = 3; // Range ranged-only invaders flee from hostiles
    private const int RangedRange = 3;
    private const int MeleeRange = 1;
    private const int HealPower = 12; // Heal power per HEAL part
    private const int RangedHealPower = 4; // Ranged heal power per HEAL part
    private const int AttackPower = 30; // Attack power per ATTACK part
    private static readonly int[] DamageByRange = [10, 10, 4, 1]; // [range 0, 1, 2, 3]

    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        var gameTime = context.State.GameTime;

        // Categorize creeps in the room
        var invaders = new List<RoomObjectSnapshot>();
        var healers = new List<RoomObjectSnapshot>();
        var hostiles = new List<RoomObjectSnapshot>();

        foreach (var obj in context.State.Objects.Values) {
            if (!obj.IsCreep())
                continue;

            if (SystemUserIds.IsInvader(obj.UserId)) {
                invaders.Add(obj);
                if (HasActiveBodyPart(obj, BodyPartType.Heal)) {
                    healers.Add(obj);
                }
            }
            else if (!SystemUserIds.IsNpcUser(obj.UserId)) {
                // Invaders attack all non-NPC creeps (ignore source keepers and other NPCs)
                hostiles.Add(obj);
            }
        }

        // Process each invader
        foreach (var invader in invaders) {
            if (HasActiveBodyPart(invader, BodyPartType.Heal)) {
                ProcessHealerAi(context, invader, invaders, healers, hostiles);
            }
            else {
                ProcessAttackerAi(context, invader, invaders, healers, hostiles);
            }

            // ShootAtWill: ranged attacks always execute after movement/healing
            ProcessShootAtWill(context, invader, hostiles);
        }

        var result = Task.CompletedTask;
        return result;
    }

    private static void ProcessHealerAi(
        RoomProcessorContext context,
        RoomObjectSnapshot healer,
        List<RoomObjectSnapshot> invaders,
        List<RoomObjectSnapshot> healers,
        List<RoomObjectSnapshot> hostiles)
    {
        // 1. Heal damaged invaders in range
        var healTargets = invaders
            .Where(i => PathCaching.GetDistance(healer, i) <= RangedRange)
            .OrderByDescending(i => (i.HitsMax ?? 0) - (i.Hits ?? 0))
            .ToList();

        if (healTargets.Count > 0) {
            var target = healTargets.First();
            var distance = PathCaching.GetDistance(healer, target);

            if (distance <= MeleeRange) {
                ApplyHeal(context, healer, target, HealPower);
            }
            else {
                ApplyHeal(context, healer, target, RangedHealPower);
            }
        }

        // 2. Flee from hostiles if damaged below 50% HP
        var currentHits = healer.Hits ?? 0;
        var maxHits = healer.HitsMax ?? 1;
        if (currentHits < maxHits / 2) {
            if (TryFlee(context, healer, hostiles, FleeRangeHealers)) {
                return;
            }

            // Move toward other healers if available
            var nearbyHealers = healers
                .Where(h => h.Id != healer.Id && HasActiveBodyPart(h, BodyPartType.Heal))
                .ToList();

            if (nearbyHealers.Count > 0) {
                var closestHealer = nearbyHealers.MinBy(h => PathCaching.GetDistance(healer, h));
                if (closestHealer is not null) {
                    MoveToward(context, healer, closestHealer);
                }
            }

            return;
        }

        // 3. Move toward damaged invaders
        var damagedTarget = invaders
            .Where(i => i.Hits < i.HitsMax)
            .MinBy(i => PathCaching.GetDistance(healer, i));

        if (damagedTarget is null) {
            // Try to flee if no damaged targets
            if (TryFlee(context, healer, hostiles, FleeRangeHealers)) {
                return;
            }

            // Move toward non-healer invaders
            var nonHealers = invaders
                .Where(i => i.Id != healer.Id && !HasActiveBodyPart(i, BodyPartType.Heal))
                .ToList();

            damagedTarget = nonHealers.FirstOrDefault();
        }

        if (damagedTarget is not null) {
            var distance = PathCaching.GetDistance(healer, damagedTarget);
            if (distance > MeleeRange) {
                MoveToward(context, healer, damagedTarget);
            }
        }
    }

    private static void ProcessAttackerAi(
        RoomProcessorContext context,
        RoomObjectSnapshot invader,
        List<RoomObjectSnapshot> invaders,
        List<RoomObjectSnapshot> healers,
        List<RoomObjectSnapshot> hostiles)
    {
        var hasAttack = HasActiveBodyPart(invader, BodyPartType.Attack);
        var hasRanged = HasActiveBodyPart(invader, BodyPartType.RangedAttack);

        // 1. Ranged-only invaders flee from hostiles
        if (!hasAttack && hasRanged && TryFlee(context, invader, hostiles, FleeRangeRanged)) {
            return;
        }

        // 2. Damaged attackers move toward healers
        var currentHits = invader.Hits ?? 0;
        var maxHits = invader.HitsMax ?? 1;
        if (currentHits < maxHits / 2 && healers.Count > 0) {
            var closestHealer = healers.MinBy(h => PathCaching.GetDistance(invader, h));
            if (closestHealer is not null) {
                var distance = PathCaching.GetDistance(invader, closestHealer);
                if (distance > 0) {
                    MoveToward(context, invader, closestHealer);
                    return;
                }
            }
        }

        // 3. Melee attack adjacent hostiles
        if (hasAttack) {
            var nearCreep = hostiles.FirstOrDefault(h => PathCaching.GetDistance(invader, h) <= MeleeRange);
            if (nearCreep is not null) {
                ApplyMeleeAttack(context, invader, nearCreep);
            }
        }

        // 4. Move toward closest hostile
        if (hostiles.Count > 0) {
            var target = hostiles.MinBy(h => PathCaching.GetDistance(invader, h));
            if (target is not null) {
                var distance = PathCaching.GetDistance(invader, target);
                if (hasAttack || distance > RangedRange) {
                    MoveToward(context, invader, target);
                }
            }
        }
    }

    private static void ProcessShootAtWill(
        RoomProcessorContext context,
        RoomObjectSnapshot invader,
        List<RoomObjectSnapshot> hostiles)
    {
        if (!HasActiveBodyPart(invader, BodyPartType.RangedAttack))
            return;

        var targets = hostiles
            .Where(h => PathCaching.GetDistance(invader, h) <= RangedRange)
            .ToList();

        if (targets.Count == 0)
            return;

        var target = targets.MinBy(h => h.Hits ?? 0);
        if (target is not null) {
            ApplyRangedAttack(context, invader, target);
        }
    }

    private static bool TryFlee(
        RoomProcessorContext context,
        RoomObjectSnapshot invader,
        List<RoomObjectSnapshot> hostiles,
        int fleeRange)
    {
        var nearHostiles = hostiles.Where(h => PathCaching.GetDistance(invader, h) < fleeRange).ToList();
        if (nearHostiles.Count == 0)
            return false;

        // Calculate flee direction (away from nearest hostile)
        var nearest = nearHostiles.MinBy(h => PathCaching.GetDistance(invader, h));
        if (nearest is null)
            return false;

        // Move away from hostile
        var dx = invader.X - nearest.X;
        var dy = invader.Y - nearest.Y;

        // Normalize and move in opposite direction
        var direction = PathCaching.GetDirectionBetween(nearest.X, nearest.Y, invader.X, invader.Y);
        if (!direction.HasValue)
            return false;

        // Apply movement in flee direction
        var (newX, newY) = CalculateNewPosition(invader.X, invader.Y, direction.Value);

        if (newX >= 0 && newX < 50 && newY >= 0 && newY < 50) {
            var patch = new RoomObjectPatchPayload
            {
                Position = new RoomObjectPositionPatch(newX, newY)
            };
            context.MutationWriter.Patch(invader.Id, patch);
            return true;
        }

        return false;
    }

    private static void MoveToward(
        RoomProcessorContext context,
        RoomObjectSnapshot invader,
        RoomObjectSnapshot target)
    {
        var direction = PathCaching.GetDirectionBetween(invader.X, invader.Y, target.X, target.Y);
        if (!direction.HasValue)
            return;

        var (newX, newY) = CalculateNewPosition(invader.X, invader.Y, direction.Value);

        if (newX >= 0 && newX < 50 && newY >= 0 && newY < 50) {
            var patch = new RoomObjectPatchPayload
            {
                Position = new RoomObjectPositionPatch(newX, newY)
            };
            context.MutationWriter.Patch(invader.Id, patch);
        }
    }

    private static (int X, int Y) CalculateNewPosition(int x, int y, Direction direction)
    {
        var (dx, dy) = direction.ToOffset();

        var result = (x + dx, y + dy);
        return result;
    }

    private static void ApplyHeal(
        RoomProcessorContext context,
        RoomObjectSnapshot healer,
        RoomObjectSnapshot target,
        int healPowerPerPart)
    {
        var healParts = healer.Body.Count(p => p.Type == BodyPartType.Heal && p.Hits > 0);
        var healing = healPowerPerPart * healParts;

        var currentHits = target.Hits ?? 0;
        var maxHits = target.HitsMax ?? 1;
        var newHits = Math.Min(maxHits, currentHits + healing);

        var patch = new RoomObjectPatchPayload
        {
            Hits = newHits
        };
        context.MutationWriter.Patch(target.Id, patch);
    }

    private static void ApplyMeleeAttack(
        RoomProcessorContext context,
        RoomObjectSnapshot invader,
        RoomObjectSnapshot target)
    {
        var attackParts = invader.Body.Count(p => p.Type == BodyPartType.Attack && p.Hits > 0);
        var damage = AttackPower * attackParts;

        var currentHits = target.Hits ?? 0;
        var newHits = Math.Max(0, currentHits - damage);

        var patch = new RoomObjectPatchPayload
        {
            Hits = newHits
        };
        context.MutationWriter.Patch(target.Id, patch);
    }

    private static void ApplyRangedAttack(
        RoomProcessorContext context,
        RoomObjectSnapshot invader,
        RoomObjectSnapshot target)
    {
        var distance = PathCaching.GetDistance(invader, target);
        var damageIndex = Math.Clamp(distance, 0, DamageByRange.Length - 1);
        var damagePerPart = DamageByRange[damageIndex];

        var rangedParts = invader.Body.Count(p => p.Type == BodyPartType.RangedAttack && p.Hits > 0);
        var damage = damagePerPart * rangedParts;

        var currentHits = target.Hits ?? 0;
        var newHits = Math.Max(0, currentHits - damage);

        var patch = new RoomObjectPatchPayload
        {
            Hits = newHits
        };
        context.MutationWriter.Patch(target.Id, patch);
    }

    private static bool HasActiveBodyPart(RoomObjectSnapshot creep, BodyPartType type)
    {
        var hasActivePart = creep.Body.Any(p => p.Type == type && p.Hits > 0);
        return hasActivePart;
    }
}
