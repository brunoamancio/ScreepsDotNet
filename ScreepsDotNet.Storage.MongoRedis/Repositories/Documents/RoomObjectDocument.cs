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

    [BsonElement("reservation")]
    public RoomReservationDocument? Reservation { get; set; }

    [BsonElement("sign")]
    public RoomSignDocument? Sign { get; set; }

    [BsonElement("safeMode")]
    public int? SafeMode { get; set; }

    [BsonElement("mineralType")]
    public string? MineralType { get; set; }

    [BsonElement("density")]
    public int? Density { get; set; }

    [BsonElement("x")]
    public int? X { get; set; }

    [BsonElement("y")]
    public int? Y { get; set; }

    [BsonElement("name")]
    public string? Name { get; set; }

    [BsonElement("store")]
    public Dictionary<string, int>? Store { get; set; }

    [BsonElement("storeCapacity")]
    public int? StoreCapacity { get; set; }

    [BsonElement("storeCapacityResource")]
    public Dictionary<string, int>? StoreCapacityResource { get; set; }

    [BsonElement("hits")]
    public int? Hits { get; set; }

    [BsonElement("hitsMax")]
    public int? HitsMax { get; set; }

    [BsonElement("ticksToLive")]
    public int? TicksToLive { get; set; }

    [BsonElement("fatigue")]
    public int? Fatigue { get; set; }

    [BsonElement("spawning")]
    public BsonValue? Spawning { get; set; }

    [BsonElement("notifyWhenAttacked")]
    public bool? NotifyWhenAttacked { get; set; }

    [BsonElement("strongholdId")]
    public string? StrongholdId { get; set; }

    [BsonElement("nextExpandTime")]
    public int? NextExpandTime { get; set; }

    [BsonElement("effects")]
    public BsonArray? Effects { get; set; }

    [BsonElement("templateName")]
    public string? TemplateName { get; set; }

    [BsonElement("depositType")]
    public string? DepositType { get; set; }

    [BsonElement("deployTime")]
    public int? DeployTime { get; set; }

    [BsonElement("strongholdBehavior")]
    public string? StrongholdBehavior { get; set; }

    [BsonElement("structure")]
    public RoomObjectStructureDocument? Structure { get; set; }

    [BsonElement("downgradeTime")]
    public long? DowngradeTime { get; set; }

    [BsonElement("progress")]
    public int? Progress { get; set; }

    [BsonElement("progressTotal")]
    public int? ProgressTotal { get; set; }

    [BsonElement("invaderGoal")]
    public int? InvaderGoal { get; set; }

    [BsonElement("nextNpcMarketOrder")]
    public int? NextNpcMarketOrder { get; set; }

    [BsonElement("status")]
    public string? Status { get; set; }

    [BsonElement("shard")]
    public string? Shard { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class RoomObjectStructureDocument
{
    [BsonElement("id")]
    public string? Id { get; set; }

    [BsonElement("type")]
    public string? Type { get; set; }

    [BsonElement("hits")]
    public int? Hits { get; set; }

    [BsonElement("hitsMax")]
    public int? HitsMax { get; set; }

    [BsonElement("user")]
    public string? UserId { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class RoomReservationDocument
{
    [BsonElement("user")]
    public string? UserId { get; set; }

    [BsonElement("endTime")]
    public int? EndTime { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class RoomSignDocument
{
    [BsonElement("user")]
    public string? UserId { get; set; }

    [BsonElement("text")]
    public string? Text { get; set; }

    [BsonElement("time")]
    public int? Time { get; set; }
}
