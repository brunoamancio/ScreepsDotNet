namespace ScreepsDotNet.Driver.Contracts;

using System.Collections.Generic;
using ScreepsDotNet.Common.Types;

public sealed record RoomIntentSnapshot(
    string RoomName,
    string? Shard,
    IReadOnlyDictionary<string, IntentEnvelope> Users);

public sealed record IntentEnvelope(
    string UserId,
    IReadOnlyDictionary<string, IReadOnlyList<IntentRecord>> ObjectIntents,
    IReadOnlyDictionary<string, SpawnIntentEnvelope> SpawnIntents,
    IReadOnlyDictionary<string, CreepIntentEnvelope> CreepIntents,
    IReadOnlyDictionary<string, TerminalIntentEnvelope>? TerminalIntents = null)
{
    public IReadOnlyDictionary<string, TerminalIntentEnvelope> TerminalIntents { get; init; } = TerminalIntents ?? new Dictionary<string, TerminalIntentEnvelope>(StringComparer.Ordinal);
}

public sealed record SpawnIntentEnvelope(
    CreateCreepIntent? CreateCreep,
    RenewCreepIntent? RenewCreep,
    RecycleCreepIntent? RecycleCreep,
    SetSpawnDirectionsIntent? SetSpawnDirections,
    bool CancelSpawning);

public sealed record CreateCreepIntent(
    string Name,
    IReadOnlyList<BodyPartType> BodyParts,
    IReadOnlyList<Direction>? Directions,
    IReadOnlyList<string>? EnergyStructureIds);

public sealed record RenewCreepIntent(string TargetId);

public sealed record RecycleCreepIntent(string TargetId);

public sealed record SetSpawnDirectionsIntent(IReadOnlyList<Direction> Directions);

public sealed record CreepIntentEnvelope(
    MoveIntent? Move,
    AttackIntent? Attack,
    AttackIntent? RangedAttack,
    HealIntent? Heal,
    IReadOnlyDictionary<string, object?> AdditionalFields);

public sealed record MoveIntent(int X, int Y);

public sealed record AttackIntent(string TargetId, int? Damage);

public sealed record HealIntent(string TargetId, int? Amount);

public sealed record TerminalIntentEnvelope(TerminalSendIntent? Send);

public sealed record TerminalSendIntent(
    string TargetRoomName,
    string ResourceType,
    int Amount,
    string? Description = null);
