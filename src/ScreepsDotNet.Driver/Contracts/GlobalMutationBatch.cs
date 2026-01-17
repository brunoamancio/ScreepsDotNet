namespace ScreepsDotNet.Driver.Contracts;

using System.Collections.Generic;

/// <summary>
/// Aggregates global-level mutations produced by the engine for driver persistence.
/// </summary>
public sealed record GlobalMutationBatch(
    IReadOnlyList<PowerCreepMutation> PowerCreepMutations)
{
    public static readonly GlobalMutationBatch Empty = new([]);
}
