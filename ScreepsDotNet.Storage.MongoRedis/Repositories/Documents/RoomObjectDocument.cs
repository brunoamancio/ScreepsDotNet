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
