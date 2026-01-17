namespace ScreepsDotNet.Storage.MongoRedis.Services;

using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

public sealed class MongoObjectNameService(IMongoDatabaseProvider databaseProvider) : IObjectNameService
{
    private const string SpawnBaseName = "Spawn";
    private readonly IMongoCollection<RoomObjectDocument> _roomObjects = databaseProvider.GetCollection<RoomObjectDocument>(databaseProvider.Settings.RoomObjectsCollection);

    public async Task<string> GenerateSpawnNameAsync(string userId, CancellationToken cancellationToken = default)
    {
        var filter = BuildSpawnFilter(userId);
        var projection = Builders<RoomObjectDocument>.Projection.Expression(doc => doc.Name);
        var documents = await _roomObjects.Find(filter)
                                          .Project(projection)
                                          .ToListAsync(cancellationToken)
                                          .ConfigureAwait(false);

        var suffix = 1;
        while (NameExists(documents, suffix))
            suffix++;

        return $"{SpawnBaseName}{suffix}";
    }

    public async Task<bool> IsSpawnNameUniqueAsync(string userId, string name, CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim();
        var filter = BuildSpawnFilter(userId) & Builders<RoomObjectDocument>.Filter.Eq(doc => doc.Name, normalized);

        var exists = await _roomObjects.Find(filter)
                                       .Limit(1)
                                       .AnyAsync(cancellationToken)
                                       .ConfigureAwait(false);
        return !exists;
    }

    private static FilterDefinition<RoomObjectDocument> BuildSpawnFilter(string userId)
    {
        var builder = Builders<RoomObjectDocument>.Filter;
        var userFilter = builder.Eq(doc => doc.UserId, userId);
        var spawnFilter = builder.Eq(doc => doc.Type, RoomObjectType.Spawn.ToDocumentValue());
        var constructionFilter = builder.And(builder.Eq(doc => doc.Type, "constructionSite"),
                                             builder.Eq(doc => doc.StructureType, RoomObjectType.Spawn.ToDocumentValue()));
        return builder.And(userFilter, builder.Or(spawnFilter, constructionFilter));
    }

    private static bool NameExists(IReadOnlyCollection<string?> names, int suffix)
    {
        var candidate = $"{SpawnBaseName}{suffix}";
        foreach (var name in names) {
            if (string.Equals(name, candidate, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
