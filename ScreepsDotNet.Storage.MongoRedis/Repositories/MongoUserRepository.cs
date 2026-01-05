using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Storage.MongoRedis.Providers;

namespace ScreepsDotNet.Storage.MongoRedis.Repositories;

public sealed class MongoUserRepository : IUserRepository
{
    private const string UsernameField = "username";
    private const string GclField = "gcl";
    private const string CpuField = "cpu";
    private const string UnknownUsername = "Unknown";

    private readonly IMongoCollection<BsonDocument> _collection;

    public MongoUserRepository(IMongoDatabaseProvider databaseProvider)
        => _collection = databaseProvider.GetCollection<BsonDocument>(databaseProvider.Settings.UsersCollection);

    public async Task<IReadOnlyCollection<UserSummary>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var documents = await _collection
                               .Find(FilterDefinition<BsonDocument>.Empty)
                               .Project(Builders<BsonDocument>.Projection.Include(UsernameField).Include(GclField).Include(CpuField))
                               .ToListAsync(cancellationToken);

        return documents.Select(document =>
                                {
                                    var username = document.TryGetValue(UsernameField, out var usernameValue) ? usernameValue.AsString : UnknownUsername;
                                    var gcl = document.TryGetValue(GclField, out var gclValue) && gclValue.IsNumeric ? gclValue.ToInt32() : 0;
                                    var cpu = document.TryGetValue(CpuField, out var cpuValue) && cpuValue.IsNumeric ? cpuValue.ToDouble() : 0d;
                                    return new UserSummary(username, gcl, cpu);
                                })
                          .ToList();
    }
}
