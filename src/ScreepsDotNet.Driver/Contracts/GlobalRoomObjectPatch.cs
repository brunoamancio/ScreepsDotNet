namespace ScreepsDotNet.Driver.Contracts;

/// <summary>
/// Represents a partial update to a room object for global processors, avoiding MongoDB-specific types in Engine layer.
/// </summary>
public sealed record GlobalRoomObjectPatch(
    int? X = null,
    int? Y = null,
    int? Hits = null,
    int? Energy = null,
    int? EnergyCapacity = null,
    int? CooldownTime = null,
    Dictionary<string, int>? Store = null,
    string? Shard = null,
    bool ClearSend = false,
    string? ObserveRoom = null,
    bool ClearObserveRoom = false);
