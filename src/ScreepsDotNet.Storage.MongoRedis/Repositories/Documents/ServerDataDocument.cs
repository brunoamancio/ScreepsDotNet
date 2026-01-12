namespace ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

using System;
using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;

[BsonIgnoreExtraElements]
public sealed class ServerDataDocument
{
    public const string DefaultId = "server-info";

    [BsonId]
    public string Id { get; set; } = DefaultId;

    [BsonElement("welcomeText")]
    public string WelcomeText { get; set; } = string.Empty;

    [BsonElement("customObjectTypes")]
    public Dictionary<string, object?> CustomObjectTypes { get; set; } = new(StringComparer.Ordinal);

    [BsonElement("historyChunkSize")]
    public int HistoryChunkSize { get; set; }

    [BsonElement("socketUpdateThrottle")]
    public int SocketUpdateThrottle { get; set; }

    [BsonElement("renderer")]
    public ServerRendererDocument Renderer { get; set; } = new();
}

[BsonIgnoreExtraElements]
public sealed class ServerRendererDocument
{
    [BsonElement("resources")]
    public Dictionary<string, object> Resources { get; set; } = new(StringComparer.Ordinal);

    [BsonElement("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new(StringComparer.Ordinal);
}
