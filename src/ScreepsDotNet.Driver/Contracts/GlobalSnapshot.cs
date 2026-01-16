namespace ScreepsDotNet.Driver.Contracts;

using System.Collections.Generic;
using ScreepsDotNet.Common.Types;

/// <summary>
/// Aggregated cross-room data consumed by the global processor/engine.
/// </summary>
public sealed record GlobalSnapshot(
    int GameTime,
    IReadOnlyList<RoomObjectSnapshot> MovingCreeps,
    IReadOnlyDictionary<string, RoomInfoSnapshot> AccessibleRooms,
    IReadOnlyList<RoomObjectSnapshot> SpecialRoomObjects,
    GlobalMarketSnapshot Market);

/// <summary>
/// Market- and user-centric data that accompanies the global snapshot.
/// </summary>
public sealed record GlobalMarketSnapshot(
    IReadOnlyList<MarketOrderSnapshot> Orders,
    IReadOnlyDictionary<string, UserState> Users,
    IReadOnlyList<PowerCreepSnapshot> PowerCreeps,
    IReadOnlyList<GlobalUserIntentSnapshot> UserIntents,
    string ShardName);

public sealed record MarketOrderSnapshot(
    string Id,
    string? UserId,
    string? Type,
    string? RoomName,
    string? ResourceType,
    long Price,
    int Amount,
    int RemainingAmount,
    int TotalAmount,
    int? CreatedTick,
    long? CreatedTimestamp,
    bool Active);

public sealed record PowerCreepSnapshot(
    string Id,
    string? UserId,
    string? Name,
    string? ClassName,
    int? Level,
    int? HitsMax,
    IReadOnlyDictionary<string, int> Store,
    int? StoreCapacity,
    long? SpawnCooldownTime,
    long? DeleteTime,
    string? Shard,
    IReadOnlyDictionary<string, PowerCreepPowerSnapshot> Powers);

public sealed record PowerCreepPowerSnapshot(int Level);

public sealed record GlobalUserIntentSnapshot(
    string Id,
    string? UserId,
    IReadOnlyList<IntentRecord> Intents);

public sealed record IntentRecord(
    string Name,
    IReadOnlyList<IntentArgument> Arguments);

public sealed record IntentArgument(
    IReadOnlyDictionary<string, IntentFieldValue> Fields);

public enum IntentFieldValueKind
{
    Text,
    Number,
    Boolean,
    TextArray,
    NumberArray,
    BodyPartArray
}

public sealed record IntentFieldValue(
    IntentFieldValueKind Kind,
    string? TextValue = null,
    int? NumberValue = null,
    bool? BooleanValue = null,
    IReadOnlyList<string>? TextValues = null,
    IReadOnlyList<int>? NumberValues = null,
    IReadOnlyList<BodyPartType>? BodyParts = null);
