namespace ScreepsDotNet.Driver.Contracts;

public sealed record MarketOrderMutation(
    string Id,
    MarketOrderMutationType Type,
    bool IsInterShard,
    MarketOrderSnapshot? Snapshot = null,
    MarketOrderPatch? Patch = null);

public enum MarketOrderMutationType
{
    Upsert,
    Patch,
    Remove
}

public sealed record MarketOrderPatch(
    bool? Active = null,
    int? Amount = null,
    int? RemainingAmount = null,
    int? TotalAmount = null,
    long? Price = null,
    int? CreatedTick = null,
    long? CreatedTimestamp = null,
    string? Type = null,
    string? ResourceType = null,
    string? RoomName = null);
