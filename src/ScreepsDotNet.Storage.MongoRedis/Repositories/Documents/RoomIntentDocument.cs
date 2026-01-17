using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;

namespace ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

[BsonIgnoreExtraElements]
public sealed class RoomIntentDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("room")]
    public string? Room { get; set; }

    [BsonElement("shard")]
    public string? Shard { get; set; }

    [BsonElement("users")]
    [BsonDictionaryOptions(DictionaryRepresentation.Document)]
    public Dictionary<string, RoomIntentUserDocument>? Users { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class RoomIntentUserDocument
{
    [BsonElement("objectsManual")]
    [BsonDictionaryOptions(DictionaryRepresentation.Document)]
    public Dictionary<string, BsonDocument>? ObjectsManual { get; set; }
}
