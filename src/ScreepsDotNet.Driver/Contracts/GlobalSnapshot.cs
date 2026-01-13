namespace ScreepsDotNet.Driver.Contracts;

using System.Collections.Generic;

/// <summary>
/// Aggregated cross-room data consumed by the global processor/engine.
/// </summary>
public sealed record GlobalSnapshot(
    int GameTime,
    IReadOnlyList<RoomObjectState> MovingCreeps,
    IReadOnlyDictionary<string, RoomInfoSnapshot> AccessibleRooms,
    IReadOnlyList<RoomObjectState> SpecialRoomObjects,
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
    bool Active,
    string RawJson);

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
    IReadOnlyDictionary<string, PowerCreepPowerSnapshot> Powers,
    string RawJson);

public sealed record PowerCreepPowerSnapshot(int Level);

public sealed record GlobalUserIntentSnapshot(
    string Id,
    string? UserId,
    string IntentsJson);
