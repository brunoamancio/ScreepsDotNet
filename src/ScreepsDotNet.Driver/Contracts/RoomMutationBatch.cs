namespace ScreepsDotNet.Driver.Contracts;

using System.Collections.Generic;
using ScreepsDotNet.Common.Types;

public sealed record RoomMutationBatch(
    string RoomName,
    IReadOnlyList<RoomObjectUpsert> ObjectUpserts,
    IReadOnlyList<RoomObjectPatch> ObjectPatches,
    IReadOnlyList<string> ObjectDeletes,
    RoomInfoPatchPayload? RoomInfoPatch,
    IRoomMapViewPayload? MapView,
    IRoomEventLogPayload? EventLog);

public sealed record RoomObjectUpsert(RoomObjectSnapshot Document);

public sealed record RoomObjectPatch(string ObjectId, IRoomObjectPatchPayload Payload);

public interface IRoomObjectPatchPayload;

public sealed record RoomObjectPatchPayload : IRoomObjectPatchPayload
{
    public int? Hits { get; init; }
    public RoomObjectPositionPatch? Position { get; init; }
    public int? Fatigue { get; init; }
    public int? DowngradeTimer { get; init; }
    public bool? UpgradeBlocked { get; init; }
    public int? SpawnCooldownTime { get; init; }
    public int? StructureHits { get; init; }
    public int? DecayTime { get; init; }
    public int? TicksToLive { get; init; }
    public int? Progress { get; init; }
    public RoomObjectActionLogPatch? ActionLog { get; init; }
    public IReadOnlyDictionary<string, int>? Store { get; init; }
    public int? StoreCapacity { get; init; }
    public IReadOnlyDictionary<string, int>? StoreCapacityResource { get; init; }
    public RoomSpawnSpawningSnapshot? Spawning { get; init; }
    public bool ClearSpawning { get; init; }
    public RoomObjectInterRoomPatch? InterRoom { get; init; }
    public IReadOnlyList<CreepBodyPartSnapshot>? Body { get; init; }
    public int? Energy { get; init; }
    public int? MineralAmount { get; init; }
    public int? InvaderHarvested { get; init; }
    public int? Harvested { get; init; }
    public int? Cooldown { get; init; }
    public int? CooldownTime { get; init; }
    public RoomReservationSnapshot? Reservation { get; init; }
    public int? SafeModeAvailable { get; init; }
    public IReadOnlyDictionary<PowerTypes, PowerEffectSnapshot>? Effects { get; init; }
    public IReadOnlyDictionary<PowerTypes, PowerCreepPowerSnapshot>? Powers { get; init; }

    public bool HasChanges =>
        Hits.HasValue ||
        (Position?.HasCoordinates ?? false) ||
        Fatigue.HasValue ||
        DowngradeTimer.HasValue ||
        UpgradeBlocked.HasValue ||
        SpawnCooldownTime.HasValue ||
        StructureHits.HasValue ||
        DecayTime.HasValue ||
        TicksToLive.HasValue ||
        Progress.HasValue ||
        (ActionLog?.HasEntries ?? false) ||
        (Store is { Count: > 0 }) ||
        StoreCapacity.HasValue ||
        (StoreCapacityResource is { Count: > 0 }) ||
        (Body is { Count: > 0 }) ||
        InterRoom is not null ||
        Energy.HasValue ||
        MineralAmount.HasValue ||
        InvaderHarvested.HasValue ||
        Harvested.HasValue ||
        Cooldown.HasValue ||
        CooldownTime.HasValue ||
        Spawning is not null ||
        ClearSpawning ||
        Reservation is not null ||
        SafeModeAvailable.HasValue ||
        (Effects is { Count: > 0 }) ||
        (Powers is { Count: > 0 });
}

public sealed record RoomObjectPositionPatch(int? X = null, int? Y = null)
{
    public bool HasCoordinates => X.HasValue || Y.HasValue;
}

public sealed record RoomObjectActionLogPatch(
    RoomObjectActionLogDie? Die = null,
    RoomObjectActionLogHealed? Healed = null,
    RoomObjectActionLogRepair? Repair = null,
    RoomObjectActionLogBuild? Build = null,
    RoomObjectActionLogHarvest? Harvest = null,
    RoomObjectActionLogRunReaction? RunReaction = null,
    RoomObjectActionLogTransferEnergy? TransferEnergy = null,
    RoomObjectActionLogProduce? Produce = null,
    RoomObjectActionLogUsePower? UsePower = null)
{
    public bool HasEntries => Die is not null || Healed is not null || Repair is not null || Build is not null || Harvest is not null || RunReaction is not null || TransferEnergy is not null || Produce is not null || UsePower is not null;
}

public sealed record RoomObjectActionLogDie(int Time);

public sealed record RoomObjectActionLogHealed(int X, int Y);

public sealed record RoomObjectActionLogRepair(int X, int Y);

public sealed record RoomObjectActionLogBuild(int X, int Y);

public sealed record RoomObjectActionLogHarvest(int X, int Y);

public sealed record RoomObjectActionLogRunReaction(int X1, int Y1, int X2, int Y2);

public sealed record RoomObjectActionLogTransferEnergy(int X, int Y);

public sealed record RoomObjectActionLogProduce(string ResourceType);

public sealed record RoomObjectActionLogUsePower(int Power, int X, int Y);

public sealed record RoomObjectActionLogSnapshot(
    RoomObjectActionLogDie? Die = null,
    RoomObjectActionLogHealed? Healed = null,
    RoomObjectActionLogRepair? Repair = null,
    RoomObjectActionLogBuild? Build = null,
    RoomObjectActionLogHarvest? Harvest = null)
{
    public bool HasEntries => Die is not null || Healed is not null || Repair is not null || Build is not null || Harvest is not null;
}

public sealed record RoomObjectInterRoomPatch(string RoomName, int X, int Y, string? Shard = null);

public sealed record RoomInfoPatchPayload
{
    public string? Status { get; init; }
    public bool? IsNoviceArea { get; init; }
    public bool? IsRespawnArea { get; init; }
    public long? OpenTime { get; init; }
    public string? OwnerUserId { get; init; }
    public ControllerLevel? ControllerLevel { get; init; }
    public int? EnergyAvailable { get; init; }
    public long? NextNpcMarketOrder { get; init; }
    public long? PowerBankTime { get; init; }
    public int? InvaderGoal { get; init; }

    public bool HasChanges =>
        Status is not null ||
        IsNoviceArea.HasValue ||
        IsRespawnArea.HasValue ||
        OpenTime.HasValue ||
        OwnerUserId is not null ||
        ControllerLevel.HasValue ||
        EnergyAvailable.HasValue ||
        NextNpcMarketOrder.HasValue ||
        PowerBankTime.HasValue ||
        InvaderGoal.HasValue;
}

public interface IRoomEventLogPayload;

public interface IRoomMapViewPayload;

public sealed record RoomIntentEventLog(string Room, int Tick, IReadOnlyList<RoomIntentEvent> Events) : IRoomEventLogPayload;

public sealed record RoomIntentMapView(string Room, long Timestamp, IReadOnlyList<RoomIntentEvent> Events) : IRoomMapViewPayload;

public sealed record RoomIntentEvent(
    string UserId,
    string ObjectId,
    RoomIntentEventKind Kind,
    SpawnIntentEnvelope? SpawnIntent,
    CreepIntentEnvelope? CreepIntent);

public enum RoomIntentEventKind
{
    Spawn,
    Creep
}
