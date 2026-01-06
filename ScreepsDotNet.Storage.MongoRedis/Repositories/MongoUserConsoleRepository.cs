using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

namespace ScreepsDotNet.Storage.MongoRedis.Repositories;

public sealed class MongoUserConsoleRepository : IUserConsoleRepository
{
    private readonly IMongoCollection<UserConsoleEntryDocument> _collection;

    public MongoUserConsoleRepository(IMongoDatabaseProvider databaseProvider)
        => _collection = databaseProvider.GetCollection<UserConsoleEntryDocument>(databaseProvider.Settings.UserConsoleCollection);

    public Task EnqueueExpressionAsync(string userId, string expression, bool hidden, CancellationToken cancellationToken = default)
    {
        var document = new UserConsoleEntryDocument
        {
            UserId = userId,
            Expression = expression,
            Hidden = hidden,
            CreatedAt = DateTime.UtcNow
        };

        return _collection.InsertOneAsync(document, cancellationToken: cancellationToken);
    }
}
