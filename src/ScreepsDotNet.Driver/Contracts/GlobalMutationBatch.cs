namespace ScreepsDotNet.Driver.Contracts;

using System.Collections.Generic;

/// <summary>
/// Aggregates global-level mutations produced by the engine for driver persistence.
/// </summary>
public sealed record GlobalMutationBatch(
    IReadOnlyList<PowerCreepMutation> PowerCreepMutations,
    IReadOnlyList<MarketOrderMutation> MarketOrderMutations,
    IReadOnlyList<UserMoneyMutation> UserMoneyMutations,
    IReadOnlyList<UserMoneyLogEntry> UserMoneyLogEntries,
    IReadOnlyList<RoomObjectMutation> RoomObjectMutations,
    IReadOnlyList<TransactionLogEntry> TransactionLogEntries,
    IReadOnlyList<UserResourceMutation> UserResourceMutations,
    IReadOnlyList<UserResourceLogEntry> UserResourceLogEntries)
{
    public static readonly GlobalMutationBatch Empty = new([], [], [], [], [], [], [], []);
}
