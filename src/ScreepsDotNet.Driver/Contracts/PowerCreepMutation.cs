namespace ScreepsDotNet.Driver.Contracts;

using System.Collections.Generic;
using ScreepsDotNet.Common.Types;

public sealed record PowerCreepMutation(
    string Id,
    PowerCreepMutationType Type,
    PowerCreepSnapshot? Snapshot = null,
    PowerCreepMutationPatch? Patch = null);

public enum PowerCreepMutationType
{
    Upsert,
    Patch,
    Remove
}

public sealed record PowerCreepMutationPatch(
    string? Name = null,
    int? Level = null,
    int? HitsMax = null,
    int? StoreCapacity = null,
    long? SpawnCooldownTime = null,
    long? DeleteTime = null,
    bool ClearDeleteTime = false,
    string? Shard = null,
    IReadOnlyDictionary<PowerTypes, PowerCreepPowerSnapshot>? Powers = null);
