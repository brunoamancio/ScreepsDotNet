namespace ScreepsDotNet.Driver.Contracts;

/// <summary>
/// Metadata about a room (status, controller level, NPC timers) serialized from the rooms collection.
/// </summary>
public sealed record RoomInfoSnapshot(
    string RoomName,
    string? Shard,
    string? Status,
    bool? IsNoviceArea,
    bool? IsRespawnArea,
    long? OpenTime,
    string? OwnerUserId,
    int? ControllerLevel,
    int? EnergyAvailable,
    long? NextNpcMarketOrder,
    long? PowerBankTime,
    int? InvaderGoal,
    string RawJson);
