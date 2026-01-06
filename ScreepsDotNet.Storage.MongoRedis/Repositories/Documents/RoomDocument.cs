namespace ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

[BsonIgnoreExtraElements]
public sealed class RoomDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("name")]
    public string? Name { get; set; }

    [BsonElement("owner")]
    public string? Owner { get; set; }

    [BsonElement("controller")]
    public RoomControllerDocument? Controller { get; set; }

    [BsonElement("energyAvailable")]
    public int? EnergyAvailable { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class RoomControllerDocument
{
    [BsonElement("level")]
    public int? Level { get; set; }
}
