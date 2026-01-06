namespace ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

[BsonIgnoreExtraElements]
public sealed class UserMoneyEntryDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("user")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("date")]
    public DateTime Date { get; set; }

    [BsonExtraElements]
    public Dictionary<string, object?> ExtraElements { get; set; } = new(StringComparer.Ordinal);
}
