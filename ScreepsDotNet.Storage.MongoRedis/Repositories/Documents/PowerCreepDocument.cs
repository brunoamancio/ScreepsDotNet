namespace ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;

[BsonIgnoreExtraElements]
public sealed class PowerCreepDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("user")]
    public string? UserId { get; set; }

    [BsonElement("name")]
    public string? Name { get; set; }

    [BsonElement("className")]
    public string? ClassName { get; set; }

    [BsonElement("level")]
    public int? Level { get; set; }

    [BsonElement("hitsMax")]
    public int? HitsMax { get; set; }

    [BsonElement("store")]
    public Dictionary<string, int>? Store { get; set; }

    [BsonElement("storeCapacity")]
    public int? StoreCapacity { get; set; }

    [BsonElement("spawnCooldownTime")]
    public long? SpawnCooldownTime { get; set; }

    [BsonElement("deleteTime")]
    public long? DeleteTime { get; set; }

    [BsonElement("shard")]
    public string? Shard { get; set; }

    [BsonElement("powers")]
    [BsonDictionaryOptions(DictionaryRepresentation.Document)]
    public Dictionary<string, PowerCreepPowerDocument>? Powers { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class PowerCreepPowerDocument
{
    [BsonElement("level")]
    public int Level { get; set; }
}
