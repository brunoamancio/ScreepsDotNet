namespace ScreepsDotNet.Driver.Contracts;

using System.Collections.Generic;

public sealed record RoomIntentSnapshot(
    string RoomName,
    string? Shard,
    IReadOnlyDictionary<string, IntentEnvelope> Users);

public sealed record IntentEnvelope(
    string UserId,
    IReadOnlyDictionary<string, IReadOnlyList<IntentRecord>> ObjectIntents,
    IReadOnlyDictionary<string, SpawnIntentEnvelope> SpawnIntents,
    IReadOnlyDictionary<string, CreepIntentEnvelope> CreepIntents);

public sealed record SpawnIntentEnvelope(
    CreateCreepIntent? CreateCreep,
    RenewCreepIntent? RenewCreep,
    RecycleCreepIntent? RecycleCreep,
    SetSpawnDirectionsIntent? SetSpawnDirections,
    bool CancelSpawning);

public sealed record CreateCreepIntent(
    string Name,
    IReadOnlyList<string> BodyParts,
    IReadOnlyList<int>? Directions,
    IReadOnlyList<string>? EnergyStructureIds);

public sealed record RenewCreepIntent(string TargetId);

public sealed record RecycleCreepIntent(string TargetId);

public sealed record SetSpawnDirectionsIntent(IReadOnlyList<int> Directions);

public sealed record CreepIntentEnvelope(
    MoveIntent? Move,
    AttackIntent? Attack,
    AttackIntent? RangedAttack,
    IReadOnlyDictionary<string, object?> AdditionalFields);

public sealed record MoveIntent(int X, int Y);

public sealed record AttackIntent(string TargetId, int? Damage);
