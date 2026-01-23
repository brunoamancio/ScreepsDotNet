using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Extensions;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Processors.Helpers;

namespace ScreepsDotNet.Engine.Processors.Steps;

/// <summary>
/// Implements source keeper AI: pathfinding, target selection, and combat.
/// Keepers patrol near assigned sources/minerals and attack hostile creeps.
/// </summary>
internal sealed class KeeperAiStep : IRoomProcessorStep
{
    private const int PathReuseTime = 50; // ticks to reuse cached path
    private const int MaxTargetDistance = 5; // tiles from keeper to source/mineral
    private const int MeleeRange = 1;
    private const int RangedRange = 3;
    private const int MassAttackThreshold = 13; // total damage threshold for mass attack
    private static readonly int[] DamageByRange = [10, 10, 4, 1]; // [range 0, 1, 2, 3]

    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        var gameTime = context.State.GameTime;

        foreach (var keeper in context.State.Objects.Values) {
            if (!IsKeeper(keeper))
                continue;

            ProcessKeeperAi(context, keeper, gameTime);
        }

        var result = Task.CompletedTask;
        return result;
    }

    private static bool IsKeeper(RoomObjectSnapshot obj)
    {
        var isKeeper = obj.Type == RoomObjectTypes.Creep &&
                       obj.UserId == NpcUserIds.SourceKeeper;
        return isKeeper;
    }

    private static void ProcessKeeperAi(RoomProcessorContext context, RoomObjectSnapshot keeper, int gameTime)
    {
        // 1. Find or assign target source/mineral
        var target = FindOrAssignTarget(context, keeper);

        // 2. Find hostile creeps in range
        var hostiles = FindHostiles(context, keeper);

        // 3. Move toward target if needed
        if (target is not null) {
            var distance = PathCaching.GetDistance(keeper, target);
            if (distance > 1)
                MoveTowardTarget(context, keeper, target, gameTime);
        }

        // 4. Attack hostiles
        if (hostiles.Count > 0) {
            AttackHostiles(context, keeper, hostiles);
        }
    }

    private static RoomObjectSnapshot? FindOrAssignTarget(RoomProcessorContext context, RoomObjectSnapshot keeper)
    {
        // Check if keeper has assigned target in memory
        if (keeper.MemorySourceId is not null) {
            if (context.State.Objects.TryGetValue(keeper.MemorySourceId, out var assigned))
                return assigned;
        }

        // Find nearest source/mineral within range
        var candidates = context.State.Objects.Values
            .Where(o => (o.Type == RoomObjectTypes.Source || o.Type == RoomObjectTypes.Mineral) &&
                        PathCaching.GetDistance(keeper, o) <= MaxTargetDistance)
            .OrderBy(o => PathCaching.GetDistance(keeper, o))
            .ToList();

        var target = candidates.FirstOrDefault();
        if (target is not null) {
            // Store target ID in keeper memory
            var patch = new RoomObjectPatchPayload
            {
                MemorySourceId = target.Id
            };
            context.MutationWriter.Patch(keeper.Id, patch);
        }

        return target;
    }

    private static List<RoomObjectSnapshot> FindHostiles(RoomProcessorContext context, RoomObjectSnapshot keeper)
    {
        var hostiles = context.State.Objects.Values
            .Where(obj => obj.IsCreep() &&
                          obj.UserId != keeper.UserId &&
                          obj.UserId != NpcUserIds.Invader &&
                          PathCaching.GetDistance(keeper, obj) <= RangedRange)
            .ToList();

        return hostiles;
    }

    private static void MoveTowardTarget(RoomProcessorContext context, RoomObjectSnapshot keeper, RoomObjectSnapshot target, int gameTime)
    {
        // Check if cached path is still valid
        var cachedPath = keeper.MemoryMove?.Path;
        var pathTime = keeper.MemoryMove?.Time;
        var pathDest = keeper.MemoryMove?.Dest;

        var targetPacked = PathCaching.PackPosition(target.X, target.Y);
        var pathValid = pathDest == targetPacked &&
                       pathTime.HasValue &&
                       gameTime <= pathTime.Value + PathReuseTime;

        Direction? direction = null;
        if (pathValid && cachedPath is not null) {
            // Reuse cached path
            direction = PathCaching.GetDirectionToFirstPathPosition(keeper, cachedPath);
        }

        if (!direction.HasValue) {
            // Calculate new path - for now, just move toward target
            // TODO: Integrate with IPathfinderService when available
            direction = PathCaching.GetDirectionBetween(keeper.X, keeper.Y, target.X, target.Y);

            if (direction.HasValue) {
                // Cache simple path (just the target position for now)
                var simplePath = PathCaching.PackPosition(target.X, target.Y);
                var memoryMove = new KeeperMoveMemory(
                    Dest: targetPacked,
                    Path: simplePath,
                    Time: gameTime,
                    LastMove: null
                );
                var patch = new RoomObjectPatchPayload
                {
                    MemoryMove = memoryMove
                };
                context.MutationWriter.Patch(keeper.Id, patch);
            }
        }

        if (direction.HasValue) {
            // Apply movement
            var (newX, newY) = CalculateNewPosition(keeper.X, keeper.Y, direction.Value);

            // Validate position is in bounds
            if (newX >= 0 && newX < 50 && newY >= 0 && newY < 50) {
                var positionPatch = new RoomObjectPatchPayload
                {
                    Position = new RoomObjectPositionPatch(newX, newY)
                };
                context.MutationWriter.Patch(keeper.Id, positionPatch);
            }
        }
    }

    private static (int X, int Y) CalculateNewPosition(int x, int y, Direction direction)
    {
        var (dx, dy) = direction.ToOffset();

        var result = (x + dx, y + dy);
        return result;
    }

    private static void AttackHostiles(RoomProcessorContext context, RoomObjectSnapshot keeper, List<RoomObjectSnapshot> hostiles)
    {
        // Melee attack: target lowest HP in range 1
        var meleeTargets = hostiles.Where(h => PathCaching.GetDistance(keeper, h) <= MeleeRange).ToList();
        if (meleeTargets.Count > 0) {
            var meleeTarget = meleeTargets.MinBy(h => h.Hits ?? 0);
            if (meleeTarget is not null) {
                ApplyMeleeAttack(context, keeper, meleeTarget);
            }
        }

        // Ranged attack: use mass attack if total damage > threshold, otherwise single target
        var rangedTargets = hostiles.Where(h => PathCaching.GetDistance(keeper, h) <= RangedRange).ToList();
        if (rangedTargets.Count > 0) {
            var rangedParts = keeper.Body.Count(p => p.Type == BodyPartType.RangedAttack);
            var totalMassDamage = rangedTargets.Sum(h => {
                var distance = PathCaching.GetDistance(keeper, h);
                var damageIndex = Math.Clamp(distance, 0, DamageByRange.Length - 1);
                var damagePerPart = DamageByRange[damageIndex];
                return damagePerPart * rangedParts;
            });

            if (totalMassDamage > MassAttackThreshold) {
                ApplyRangedMassAttack(context, keeper, rangedTargets);
            }
            else {
                var rangedTarget = rangedTargets.MinBy(h => h.Hits ?? 0);
                if (rangedTarget is not null) {
                    ApplyRangedAttack(context, keeper, rangedTarget);
                }
            }
        }
    }

    private static void ApplyMeleeAttack(RoomProcessorContext context, RoomObjectSnapshot keeper, RoomObjectSnapshot target)
    {
        const int attackPower = 30; // Keeper attack damage
        var workParts = keeper.Body.Count(p => p.Type == BodyPartType.Attack);
        var damage = attackPower * workParts;

        var currentHits = target.Hits ?? 0;
        var newHits = Math.Max(0, currentHits - damage);

        var patch = new RoomObjectPatchPayload
        {
            Hits = newHits
        };
        context.MutationWriter.Patch(target.Id, patch);
    }

    private static void ApplyRangedAttack(RoomProcessorContext context, RoomObjectSnapshot keeper, RoomObjectSnapshot target)
    {
        var distance = PathCaching.GetDistance(keeper, target);
        var damageIndex = Math.Clamp(distance, 0, DamageByRange.Length - 1);
        var damagePerPart = DamageByRange[damageIndex];

        var rangedParts = keeper.Body.Count(p => p.Type == BodyPartType.RangedAttack);
        var damage = damagePerPart * rangedParts;

        var currentHits = target.Hits ?? 0;
        var newHits = Math.Max(0, currentHits - damage);

        var patch = new RoomObjectPatchPayload
        {
            Hits = newHits
        };
        context.MutationWriter.Patch(target.Id, patch);
    }

    private static void ApplyRangedMassAttack(RoomProcessorContext context, RoomObjectSnapshot keeper, List<RoomObjectSnapshot> targets)
    {
        // Mass attack applies damage to all targets in range
        foreach (var target in targets) {
            var distance = PathCaching.GetDistance(keeper, target);
            var damageIndex = Math.Clamp(distance, 0, DamageByRange.Length - 1);
            var damagePerPart = DamageByRange[damageIndex];

            var rangedParts = keeper.Body.Count(p => p.Type == BodyPartType.RangedAttack);
            var damage = damagePerPart * rangedParts;

            var currentHits = target.Hits ?? 0;
            var newHits = Math.Max(0, currentHits - damage);

            var patch = new RoomObjectPatchPayload
            {
                Hits = newHits
            };
            context.MutationWriter.Patch(target.Id, patch);
        }
    }
}
