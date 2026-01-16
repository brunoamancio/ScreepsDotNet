namespace ScreepsDotNet.Driver.Contracts;

using System.Collections.Generic;

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
    public int? TicksToLive { get; init; }
    public RoomObjectActionLogPatch? ActionLog { get; init; }
    public IReadOnlyDictionary<string, int>? Store { get; init; }
    public RoomSpawnSpawningSnapshot? Spawning { get; init; }
    public bool ClearSpawning { get; init; }
    public IReadOnlyList<CreepBodyPartSnapshot>? Body { get; init; }

    public bool HasChanges =>
        Hits.HasValue ||
        (Position?.HasCoordinates ?? false) ||
        Fatigue.HasValue ||
        DowngradeTimer.HasValue ||
        UpgradeBlocked.HasValue ||
        SpawnCooldownTime.HasValue ||
        StructureHits.HasValue ||
        TicksToLive.HasValue ||
        (ActionLog?.HasEntries ?? false) ||
        (Store is { Count: > 0 }) ||
        (Body is { Count: > 0 }) ||
        Spawning is not null ||
        ClearSpawning;
}

public sealed record RoomObjectPositionPatch(int? X = null, int? Y = null)
{
    public bool HasCoordinates => X.HasValue || Y.HasValue;
}

public sealed record RoomObjectActionLogPatch(RoomObjectActionLogDie? Die = null)
{
    public bool HasEntries => Die is not null;
}

public sealed record RoomObjectActionLogDie(int Time);

public sealed record RoomInfoPatchPayload
{
    public string? Status { get; init; }
    public bool? IsNoviceArea { get; init; }
    public bool? IsRespawnArea { get; init; }
    public long? OpenTime { get; init; }
    public string? OwnerUserId { get; init; }
    public int? ControllerLevel { get; init; }
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
