namespace ScreepsDotNet.Storage.MongoRedis.Seeding;

using MongoDB.Driver;

public interface ISeedDataService
{
    Task ReseedAsync(string mongoConnectionString, string databaseName, CancellationToken cancellationToken = default);

    Task ReseedAsync(IMongoDatabase database, CancellationToken cancellationToken = default);
}
