namespace ScreepsDotNet.Driver.Contracts;

using System.Collections.Generic;
using ScreepsDotNet.Common;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;

/// <summary>
/// Canonical representation of a room object used by the engine/processor.
/// </summary>
public sealed record RoomObjectSnapshot(
    string Id,
    string Type,
    string RoomName,
    string? Shard,
    string? UserId,
    int X,
    int Y,
    int? Hits,
    int? HitsMax,
    int? Fatigue,
    int? TicksToLive,
    string? Name,
    int? Level,
    int? Density,
    string? MineralType,
    string? DepositType,
    string? StructureType,
    IReadOnlyDictionary<string, int> Store,
    int? StoreCapacity,
    IReadOnlyDictionary<string, int> StoreCapacityResource,
    RoomReservationSnapshot? Reservation,
    RoomSignSnapshot? Sign,
    RoomObjectStructureSnapshot? Structure,
    IReadOnlyDictionary<string, object?> Effects,
    RoomSpawnSpawningSnapshot? Spawning,
    IReadOnlyList<CreepBodyPartSnapshot> Body,
    bool? IsSpawning = null,
    bool? UserSummoned = null,
    string? StrongholdId = null,
    int? DeathTime = null,
    int? DecayTime = null,
    string? CreepId = null,
    string? CreepName = null,
    int? CreepTicksToLive = null,
    string? CreepSaying = null,
    string? ResourceType = null,
    int? ResourceAmount = null)
{
    public int? MoveBodyParts => GetStoreValue(IntentKeys.Move);
    public int? ControllerDowngradeTimer => GetStoreValue(StoreKeys.DowngradeTimer);
    public int? SpawnCooldownTime => GetStoreValue(StoreKeys.SpawnCooldownTime);

    private int? GetStoreValue(string key)
        => Store.TryGetValue(key, out var value) ? value : null;

    private static class StoreKeys
    {
        public const string DowngradeTimer = RoomDocumentFields.RoomObject.DowngradeTimer;
        public const string SpawnCooldownTime = RoomDocumentFields.RoomObject.SpawnCooldownTime;
    }
}

public sealed record RoomReservationSnapshot(string? UserId, int? EndTime);

public sealed record RoomSignSnapshot(string? UserId, string? Text, int? Time);

public sealed record RoomObjectStructureSnapshot(string? Id, string? Type, string? UserId, int? Hits, int? HitsMax);

public sealed record RoomSpawnSpawningSnapshot(
    string Name,
    int? NeedTime,
    int? SpawnTime,
    IReadOnlyList<Direction> Directions);

public sealed record CreepBodyPartSnapshot(BodyPartType Type, int Hits, string? Boost);
