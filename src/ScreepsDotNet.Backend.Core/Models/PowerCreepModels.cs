namespace ScreepsDotNet.Backend.Core.Models;

public sealed record PowerCreepListItem(
    string Id,
    string? Name,
    string ClassName,
    int Level,
    int HitsMax,
    IReadOnlyDictionary<string, int> Store,
    int StoreCapacity,
    long? SpawnCooldownTime,
    long? DeleteTime,
    string? Shard,
    IReadOnlyDictionary<string, int> Powers,
    string? Room,
    int? X,
    int? Y,
    int? Hits,
    int? Fatigue,
    int? TicksToLive);
