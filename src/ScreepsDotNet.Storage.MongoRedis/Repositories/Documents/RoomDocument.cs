namespace ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using ScreepsDotNet.Common.Types;

[BsonIgnoreExtraElements]
public sealed class RoomDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("shard")]
    public string? Shard { get; set; }

    [BsonElement("status")]
    public string? Status { get; set; }

    [BsonElement("novice")]
    public bool? Novice { get; set; }

    [BsonElement("respawnArea")]
    public bool? RespawnArea { get; set; }

    [BsonElement("openTime")]
    public long? OpenTime { get; set; }

    [BsonElement("owner")]
    public string? Owner { get; set; }

    [BsonElement("controller")]
    public RoomControllerDocument? Controller { get; set; }

    [BsonElement("energyAvailable")]
    public int? EnergyAvailable { get; set; }

    [BsonElement("nextNpcMarketOrder")]
    public long? NextNpcMarketOrder { get; set; }

    [BsonElement("powerBankTime")]
    public long? PowerBankTime { get; set; }

    [BsonElement("invaderGoal")]
    public int? InvaderGoal { get; set; }

    [BsonIgnore]
    public string? Name
    {
        get => Id;
        set { }
    }
}

[BsonIgnoreExtraElements]
public sealed class RoomControllerDocument
{
    [BsonElement("level")]
    [BsonRepresentation(BsonType.Int32)]
    public ControllerLevel? Level { get; set; }
}
