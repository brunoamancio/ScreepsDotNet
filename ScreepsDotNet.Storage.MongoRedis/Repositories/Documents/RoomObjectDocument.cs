namespace ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

/// <summary>
/// Represents the MongoDB document stored in the <c>rooms.objects</c> collection.
/// </summary>
public sealed class RoomObjectDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("user")]
    public string? UserId { get; set; }

    [BsonElement("type")]
    public string? Type { get; set; }

    [BsonElement("room")]
    public string? Room { get; set; }

    [BsonElement("level")]
    public int? Level { get; set; }
}
