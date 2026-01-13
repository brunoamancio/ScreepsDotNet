namespace ScreepsDotNet.Driver.Contracts;

using System.Collections.Generic;

/// <summary>
/// Canonical representation of a room object used by the engine/processor.
/// </summary>
public sealed record RoomObjectState(
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
    string RawJson);

public sealed record RoomReservationSnapshot(string? UserId, int? EndTime);

public sealed record RoomSignSnapshot(string? UserId, string? Text, int? Time);

public sealed record RoomObjectStructureSnapshot(string? Id, string? Type, string? UserId, int? Hits, int? HitsMax);
