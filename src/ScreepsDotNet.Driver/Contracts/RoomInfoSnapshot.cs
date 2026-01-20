namespace ScreepsDotNet.Driver.Contracts;

using ScreepsDotNet.Common.Types;

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
    ControllerLevel? ControllerLevel,
    int? EnergyAvailable,
    long? NextNpcMarketOrder,
    long? PowerBankTime,
    int? InvaderGoal);
