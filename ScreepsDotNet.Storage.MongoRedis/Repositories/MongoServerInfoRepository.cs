using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Storage.MongoRedis.Options;

namespace ScreepsDotNet.Storage.MongoRedis.Repositories;

public sealed class MongoServerInfoRepository : IServerInfoRepository
{
    private const string IdField = "_id";
    private const string NameField = "name";
    private const string BuildField = "build";
    private const string CliEnabledField = "cliEnabled";

    private const string DefaultServerName = "Unknown";
    private const string DefaultBuild = "n/a";

    private readonly IMongoCollection<BsonDocument> _collection;
    private readonly MongoRedisStorageOptions _options;

    public MongoServerInfoRepository(IOptions<MongoRedisStorageOptions> options)
    {
        _options = options.Value;
        var client = new MongoClient(_options.MongoConnectionString);
        var database = client.GetDatabase(_options.MongoDatabase);
        _collection = database.GetCollection<BsonDocument>(_options.ServerInfoCollection);
    }

    public ServerInfo GetServerInfo()
    {
        var filter = Builders<BsonDocument>.Filter.Eq(IdField, _options.ServerInfoDocumentId);
        var document = _collection.Find(filter).FirstOrDefault();

        if (document is null)
            return new ServerInfo(DefaultServerName, DefaultBuild, false);

        var name = document.TryGetValue(NameField, out var nameValue) ? nameValue.AsString : DefaultServerName;
        var build = document.TryGetValue(BuildField, out var buildValue) ? buildValue.AsString : DefaultBuild;
        var cliEnabled = document.TryGetValue(CliEnabledField, out var cliValue) && cliValue.IsBoolean && cliValue.AsBoolean;

        return new ServerInfo(name, build, cliEnabled);
    }
}
