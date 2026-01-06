namespace ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

[BsonIgnoreExtraElements]
public sealed class MarketStatsDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("resourceType")]
    public string ResourceType { get; set; } = string.Empty;

    [BsonElement("date")]
    public string Date { get; set; } = string.Empty;

    [BsonElement("transactions")]
    public int Transactions { get; set; }

    [BsonElement("volume")]
    public double Volume { get; set; }

    [BsonElement("avgPrice")]
    public double AveragePrice { get; set; }

    [BsonElement("stddevPrice")]
    public double StandardDeviation { get; set; }
}
