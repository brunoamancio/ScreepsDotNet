namespace ScreepsDotNet.Driver.Contracts;

using System.Collections.Generic;

public sealed record PowerCreepState(
    string Id,
    string? UserId,
    string Name,
    string ClassName,
    int Level,
    int? HitsMax,
    int? SpawnCooldownTime,
    int? DeleteTime,
    string? Shard,
    IReadOnlyDictionary<string, PowerCreepPowerSnapshot> Powers,
    string RawJson);

public sealed record PowerCreepPowerSnapshot(string PowerId, int Level);
