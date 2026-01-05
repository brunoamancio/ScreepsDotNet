using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Storage.MongoRedis.Providers;

namespace ScreepsDotNet.Storage.MongoRedis.Repositories;

public sealed class MongoUserCodeRepository : IUserCodeRepository
{
    private const string UserField = "user";
    private const string BranchField = "branch";
    private const string ModulesField = "modules";
    private const string TimestampField = "timestamp";
    private const string ActiveWorldField = "activeWorld";
    private const string ActiveSimField = "activeSim";
    private const string DefaultBranchName = "default";
    private const string DefaultModuleName = "main";
    private const string ActivePrefix = "$";
    private const string ActiveWorldIdentifier = "$activeWorld";
    private const string ActiveSimIdentifier = "$activeSim";

    private readonly IMongoCollection<BsonDocument> _collection;

    public MongoUserCodeRepository(IMongoDatabaseProvider databaseProvider)
        => _collection = databaseProvider.GetCollection<BsonDocument>(databaseProvider.Settings.UserCodeCollection);

    public async Task<IReadOnlyCollection<UserCodeBranch>> GetBranchesAsync(string userId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.Eq(UserField, userId);
        var branches = await _collection.Find(filter).ToListAsync(cancellationToken).ConfigureAwait(false);

        if (branches.All(document => !string.Equals(document.GetValue(BranchField, BsonNull.Value).AsString, DefaultBranchName, StringComparison.Ordinal)))
        {
            await CreateDefaultBranchAsync(userId, cancellationToken).ConfigureAwait(false);
            branches = await _collection.Find(filter).ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        return branches.Select(ToBranch).ToList();
    }

    public async Task<UserCodeBranch?> GetBranchAsync(string userId, string branchIdentifier, CancellationToken cancellationToken = default)
    {
        var branchFilter = BuildBranchFilter(userId, branchIdentifier);
        if (branchFilter is null)
            return null;

        var document = await _collection.Find(branchFilter).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return document is null ? null : ToBranch(document);
    }

    private static FilterDefinition<BsonDocument>? BuildBranchFilter(string userId, string branchIdentifier)
    {
        var userFilter = Builders<BsonDocument>.Filter.Eq(UserField, userId);

        if (string.IsNullOrWhiteSpace(branchIdentifier))
            return Builders<BsonDocument>.Filter.And(userFilter, Builders<BsonDocument>.Filter.Eq(BranchField, DefaultBranchName));

        if (branchIdentifier.StartsWith(ActivePrefix, StringComparison.Ordinal))
        {
            var fieldName = branchIdentifier.Equals(ActiveWorldIdentifier, StringComparison.OrdinalIgnoreCase)
                ? ActiveWorldField
                : branchIdentifier.Equals(ActiveSimIdentifier, StringComparison.OrdinalIgnoreCase)
                    ? ActiveSimField
                    : null;

            if (fieldName is null)
                return null;

            return Builders<BsonDocument>.Filter.And(userFilter, Builders<BsonDocument>.Filter.Eq(fieldName, true));
        }

        return Builders<BsonDocument>.Filter.And(userFilter, Builders<BsonDocument>.Filter.Eq(BranchField, branchIdentifier));
    }

    private static UserCodeBranch ToBranch(BsonDocument document)
    {
        var modules = new Dictionary<string, string>(StringComparer.Ordinal);
        if (document.TryGetValue(ModulesField, out var modulesValue) && modulesValue is BsonDocument modulesDocument)
        {
            foreach (var element in modulesDocument.Elements)
                modules[element.Name] = element.Value.AsString;
        }

        return new UserCodeBranch(
            document.GetValue(BranchField, DefaultBranchName).AsString,
            modules,
            document.TryGetValue(TimestampField, out var timestampValue) && timestampValue.IsNumeric
                ? DateTimeOffset.FromUnixTimeMilliseconds(timestampValue.ToInt64()).UtcDateTime
                : null,
            document.TryGetValue(ActiveWorldField, out var activeWorldValue) && activeWorldValue.ToBoolean(),
            document.TryGetValue(ActiveSimField, out var activeSimValue) && activeSimValue.ToBoolean());
    }

    private Task CreateDefaultBranchAsync(string userId, CancellationToken cancellationToken)
    {
        var document = new BsonDocument
        {
            { UserField, userId },
            { BranchField, DefaultBranchName },
            { ModulesField, new BsonDocument { { DefaultModuleName, string.Empty } } },
            { TimestampField, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
            { ActiveWorldField, true },
            { ActiveSimField, false }
        };

        return _collection.InsertOneAsync(document, cancellationToken: cancellationToken);
    }
}
