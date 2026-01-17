namespace ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using ScreepsDotNet.Common.Constants;
using MongoDB.Bson.Serialization.Options;

[BsonIgnoreExtraElements]
public sealed class PowerCreepDocument
{
    [BsonId]
    [BsonElement(PowerCreepDocumentFields.Id)]
    public ObjectId Id { get; set; }

    [BsonElement(PowerCreepDocumentFields.UserId)]
    public string? UserId { get; set; }

    [BsonElement(PowerCreepDocumentFields.Name)]
    public string? Name { get; set; }

    [BsonElement(PowerCreepDocumentFields.ClassName)]
    public string? ClassName { get; set; }

    [BsonElement(PowerCreepDocumentFields.Level)]
    public int? Level { get; set; }

    [BsonElement(PowerCreepDocumentFields.HitsMax)]
    public int? HitsMax { get; set; }

    [BsonElement(PowerCreepDocumentFields.Store)]
    public Dictionary<string, int>? Store { get; set; }

    [BsonElement(PowerCreepDocumentFields.StoreCapacity)]
    public int? StoreCapacity { get; set; }

    [BsonElement(PowerCreepDocumentFields.SpawnCooldownTime)]
    public long? SpawnCooldownTime { get; set; }

    [BsonElement(PowerCreepDocumentFields.DeleteTime)]
    public long? DeleteTime { get; set; }

    [BsonElement(PowerCreepDocumentFields.Shard)]
    public string? Shard { get; set; }

    [BsonElement(PowerCreepDocumentFields.Powers)]
    [BsonDictionaryOptions(DictionaryRepresentation.Document)]
    public Dictionary<string, PowerCreepPowerDocument>? Powers { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class PowerCreepPowerDocument
{
    [BsonElement("level")]
    public int Level { get; set; }
}
