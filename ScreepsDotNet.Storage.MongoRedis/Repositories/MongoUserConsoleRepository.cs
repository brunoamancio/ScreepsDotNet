using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Storage.MongoRedis.Providers;

namespace ScreepsDotNet.Storage.MongoRedis.Repositories;

public sealed class MongoUserConsoleRepository : IUserConsoleRepository
{
    private const string UserField = "user";
    private const string ExpressionField = "expression";
    private const string HiddenField = "hidden";
    private const string CreatedAtField = "createdAt";

    private readonly IMongoCollection<BsonDocument> _collection;

    public MongoUserConsoleRepository(IMongoDatabaseProvider databaseProvider)
        => _collection = databaseProvider.GetCollection<BsonDocument>(databaseProvider.Settings.UserConsoleCollection);

    public Task EnqueueExpressionAsync(string userId, string expression, bool hidden, CancellationToken cancellationToken = default)
    {
        var document = new BsonDocument
        {
            { UserField, userId },
            { ExpressionField, expression },
            { HiddenField, hidden },
            { CreatedAtField, DateTime.UtcNow }
        };

        return _collection.InsertOneAsync(document, cancellationToken: cancellationToken);
    }
}
