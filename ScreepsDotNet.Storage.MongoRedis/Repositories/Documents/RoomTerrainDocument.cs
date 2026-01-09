namespace ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

[BsonIgnoreExtraElements]
public sealed class RoomTerrainDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("room")]
    public string Room { get; set; } = string.Empty;

    [BsonElement("shard")]
    public string? Shard { get; set; }

    [BsonElement("type")]
    public string? Type { get; set; }

    [BsonElement("terrain")]
    public string? Terrain { get; set; }
}
