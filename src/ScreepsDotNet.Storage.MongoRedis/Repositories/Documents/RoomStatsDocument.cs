namespace ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

[BsonIgnoreExtraElements]
public sealed class RoomStatsDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("room")]
    public string Room { get; set; } = string.Empty;

    [BsonElement("tick")]
    public int Tick { get; set; }

    [BsonElement("timestamp")]
    public DateTime TimestampUtc { get; set; }

    [BsonElement("metrics")]
    public Dictionary<string, Dictionary<string, int>> Metrics { get; set; } = new(StringComparer.Ordinal);
}
