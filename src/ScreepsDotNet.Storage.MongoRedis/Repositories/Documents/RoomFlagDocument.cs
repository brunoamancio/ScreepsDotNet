namespace ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

using MongoDB.Bson.Serialization.Attributes;

[BsonIgnoreExtraElements]
public sealed class RoomFlagDocument
{
    [BsonId]
    public string? Id { get; set; }

    [BsonElement("user")]
    public string? UserId { get; set; }

    [BsonElement("room")]
    public string? Room { get; set; }

    [BsonElement("shard")]
    public string? Shard { get; set; }

    [BsonElement("data")]
    public string? Data { get; set; }
}
