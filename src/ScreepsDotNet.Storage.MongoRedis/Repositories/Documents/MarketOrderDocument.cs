namespace ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

[BsonIgnoreExtraElements]
public sealed class MarketOrderDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("active")]
    public bool Active { get; set; }

    [BsonElement("user")]
    public string? UserId { get; set; }

    [BsonElement("type")]
    public string? Type { get; set; }

    [BsonElement("roomName")]
    public string? RoomName { get; set; }

    [BsonElement("resourceType")]
    public string? ResourceType { get; set; }

    [BsonElement("price")]
    public long Price { get; set; }

    [BsonElement("amount")]
    public int Amount { get; set; }

    [BsonElement("remainingAmount")]
    public int RemainingAmount { get; set; }

    [BsonElement("totalAmount")]
    public int TotalAmount { get; set; }

    [BsonElement("created")]
    public int? CreatedTick { get; set; }

    [BsonElement("createdTimestamp")]
    public long? CreatedTimestamp { get; set; }
}
