namespace ScreepsDotNet.Common.Constants;

/// <summary>
/// Canonical Mongo document field names for market orders.
/// </summary>
public static class MarketOrderDocumentFields
{
    public const string Id = "_id";
    public const string Active = "active";
    public const string UserId = "user";
    public const string Type = "type";
    public const string RoomName = "roomName";
    public const string ResourceType = "resourceType";
    public const string Price = "price";
    public const string Amount = "amount";
    public const string RemainingAmount = "remainingAmount";
    public const string TotalAmount = "totalAmount";
    public const string CreatedTick = "created";
    public const string CreatedTimestamp = "createdTimestamp";
}
