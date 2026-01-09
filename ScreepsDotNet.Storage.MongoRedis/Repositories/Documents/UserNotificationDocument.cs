namespace ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

[BsonIgnoreExtraElements]
public sealed class UserNotificationDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("user")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("message")]
    public string Message { get; set; } = string.Empty;

    [BsonElement("date")]
    public long Date { get; set; }

    [BsonElement("type")]
    public string Type { get; set; } = string.Empty;

    [BsonElement("count")]
    public int Count { get; set; }
}
