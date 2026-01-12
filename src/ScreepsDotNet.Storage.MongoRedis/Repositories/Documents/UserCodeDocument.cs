namespace ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

[BsonIgnoreExtraElements]
public sealed class UserCodeDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("user")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("branch")]
    public string? Branch { get; set; }

    [BsonElement("modules")]
    public Dictionary<string, string>? Modules { get; set; }

    [BsonElement("timestamp")]
    public long? Timestamp { get; set; }

    [BsonElement("activeWorld")]
    public bool? ActiveWorld { get; set; }

    [BsonElement("activeSim")]
    public bool? ActiveSim { get; set; }
}
