using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;

namespace ScreepsDotNet.Engine.Processors.Steps;

/// <summary>
/// Spawns source keeper creeps from keeper lairs when timers expire or when no keeper is guarding the lair.
/// </summary>
internal sealed class KeeperLairStep : IRoomProcessorStep
{
    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        var gameTime = context.State.GameTime;

        foreach (var lair in context.State.Objects.Values) {
            if (lair.Type != RoomObjectTypes.KeeperLair)
                continue;

            ProcessKeeperLair(context, lair, gameTime);
        }

        var result = Task.CompletedTask;
        return result;
    }

    private static void ProcessKeeperLair(RoomProcessorContext context, RoomObjectSnapshot lair, int gameTime)
    {
        // Check if spawn timer needs to be set
        if (!lair.NextRegenerationTime.HasValue) {
            var keeper = FindKeeper(context.State.Objects, lair.Id);
            var shouldSetTimer = keeper is null || keeper.Hits < 5000;
            if (shouldSetTimer) {
                var patch = new RoomObjectPatchPayload
                {
                    NextRegenerationTime = gameTime + ScreepsGameConstants.EnergyRegenTime
                };
                context.MutationWriter.Patch(lair.Id, patch);
            }
            return;
        }

        // Check if spawn timer has expired
        var timerExpired = gameTime >= lair.NextRegenerationTime.Value - 1;
        if (timerExpired) {
            // Remove old keeper if exists
            var oldKeeper = FindKeeper(context.State.Objects, lair.Id);
            if (oldKeeper is not null)
                context.MutationWriter.Remove(oldKeeper.Id);

            // Spawn new keeper
            var newKeeper = CreateKeeper(lair, gameTime);
            context.MutationWriter.Upsert(newKeeper);

            // Clear spawn timer
            var patch = new RoomObjectPatchPayload
            {
                NextRegenerationTime = null
            };
            context.MutationWriter.Patch(lair.Id, patch);
        }
    }

    private static RoomObjectSnapshot? FindKeeper(IReadOnlyDictionary<string, RoomObjectSnapshot> objects, string lairId)
    {
        var keeperName = $"Keeper{lairId}";
        foreach (var obj in objects.Values) {
            var isKeeper = obj.Type == RoomObjectTypes.Creep &&
                           obj.UserId == NpcUserIds.SourceKeeper &&
                           obj.Name == keeperName;
            if (isKeeper)
                return obj;
        }
        return null;
    }

    private static RoomObjectSnapshot CreateKeeper(RoomObjectSnapshot lair, int gameTime)
    {
        // Build keeper body: 17 TOUGH + 13 MOVE + 10 ATTACK + 10 RANGED_ATTACK (50 parts total)
        var body = new List<CreepBodyPartSnapshot>(50);

        // 17 TOUGH
        for (var i = 0; i < 17; i++)
            body.Add(new CreepBodyPartSnapshot(BodyPartType.Tough, ScreepsGameConstants.BodyPartHitPoints, null));

        // 13 MOVE
        for (var i = 0; i < 13; i++)
            body.Add(new CreepBodyPartSnapshot(BodyPartType.Move, ScreepsGameConstants.BodyPartHitPoints, null));

        // 10 ATTACK + 10 RANGED_ATTACK (interleaved)
        for (var i = 0; i < 10; i++) {
            body.Add(new CreepBodyPartSnapshot(BodyPartType.Attack, ScreepsGameConstants.BodyPartHitPoints, null));
            body.Add(new CreepBodyPartSnapshot(BodyPartType.RangedAttack, ScreepsGameConstants.BodyPartHitPoints, null));
        }

        var keeper = new RoomObjectSnapshot(
            Id: Guid.NewGuid().ToString(),
            Type: RoomObjectTypes.Creep,
            RoomName: lair.RoomName,
            Shard: lair.Shard,
            UserId: NpcUserIds.SourceKeeper,
            X: lair.X,
            Y: lair.Y,
            Hits: 5000,
            HitsMax: 5000,
            Fatigue: 0,
            TicksToLive: null,
            Name: $"Keeper{lair.Id}",
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: null,
            Store: new Dictionary<string, int>(StringComparer.Ordinal) { [ResourceTypes.Energy] = 0 },
            StoreCapacity: 0,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: body,
            IsSpawning: false);

        return keeper;
    }
}
