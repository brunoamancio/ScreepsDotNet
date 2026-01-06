

using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Storage.MongoRedis.Extensions;
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
    private const int MaxBranchCount = 30;

    private readonly IMongoCollection<BsonDocument> _collection;

    public MongoUserCodeRepository(IMongoDatabaseProvider databaseProvider)
        => _collection = databaseProvider.GetCollection<BsonDocument>(databaseProvider.Settings.UserCodeCollection);

    public async Task<IReadOnlyCollection<UserCodeBranch>> GetBranchesAsync(string userId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.Eq(UserField, userId);
        var branches = await _collection.Find(filter).ToListAsync(cancellationToken).ConfigureAwait(false);

        if (branches.All(document => {
                var resolveBranchName = ResolveBranchName(document);
                return !string.Equals(resolveBranchName, DefaultBranchName, StringComparison.Ordinal);
            }))
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

    public async Task<bool> UpdateBranchModulesAsync(string userId, string branchIdentifier, IDictionary<string, string> modules, CancellationToken cancellationToken = default)
    {
        var filter = BuildBranchFilter(userId, branchIdentifier);
        if (filter is null)
            return false;

        var update = Builders<BsonDocument>.Update
            .Set(ModulesField, modules.ToModulesDocument())
            .Set(TimestampField, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> SetActiveBranchAsync(string userId, string branchName, string activeName, CancellationToken cancellationToken = default)
    {
        var targetField = activeName.Equals(ActiveWorldField, StringComparison.OrdinalIgnoreCase)
            ? ActiveWorldField
            : activeName.Equals(ActiveSimField, StringComparison.OrdinalIgnoreCase)
                ? ActiveSimField
                : null;

        if (targetField is null)
            return false;

        var userFilter = Builders<BsonDocument>.Filter.Eq(UserField, userId);
        var branchFilter = Builders<BsonDocument>.Filter.And(userFilter, Builders<BsonDocument>.Filter.Eq(BranchField, branchName));

        var update = Builders<BsonDocument>.Update
            .Set(targetField, true)
            .Set(TimestampField, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var result = await _collection.UpdateOneAsync(branchFilter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result.ModifiedCount == 0)
            return false;

        var resetFilter = Builders<BsonDocument>.Filter.And(userFilter, Builders<BsonDocument>.Filter.Ne(BranchField, branchName));
        var resetUpdate = Builders<BsonDocument>.Update.Set(targetField, false);
        await _collection.UpdateManyAsync(resetFilter, resetUpdate, cancellationToken: cancellationToken).ConfigureAwait(false);

        return true;
    }

    public async Task<bool> CloneBranchAsync(string userId, string? sourceBranch, string newBranchName, IDictionary<string, string>? defaultModules, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newBranchName) || newBranchName.Length > 30)
            return false;

        var userFilter = Builders<BsonDocument>.Filter.Eq(UserField, userId);
        var existingFilter = Builders<BsonDocument>.Filter.And(userFilter, Builders<BsonDocument>.Filter.Eq(BranchField, newBranchName));
        var existing = await _collection.Find(existingFilter).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (existing is not null)
            return false;

        var branchCount = await _collection.CountDocumentsAsync(userFilter, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (branchCount >= MaxBranchCount)
            return false;

        IReadOnlyDictionary<string, string>? modules = null;
        if (!string.IsNullOrWhiteSpace(sourceBranch))
        {
            var source = await GetBranchAsync(userId, sourceBranch, cancellationToken).ConfigureAwait(false);
            modules = source?.Modules;
        }

        modules ??= defaultModules is not null
            ? new Dictionary<string, string>(defaultModules, StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal) { [DefaultModuleName] = string.Empty };

        var document = new BsonDocument
        {
            { UserField, userId },
            { BranchField, newBranchName },
            { ModulesField, modules.ToMutableModuleDictionary().ToModulesDocument() },
            { TimestampField, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
            { ActiveWorldField, false },
            { ActiveSimField, false }
        };

        await _collection.InsertOneAsync(document, cancellationToken: cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> DeleteBranchAsync(string userId, string branchName, CancellationToken cancellationToken = default)
    {
        var userFilter = Builders<BsonDocument>.Filter.Eq(UserField, userId);
        var deleteFilter = Builders<BsonDocument>.Filter.And(
            userFilter,
            Builders<BsonDocument>.Filter.Eq(BranchField, branchName),
            Builders<BsonDocument>.Filter.Ne(ActiveWorldField, true),
            Builders<BsonDocument>.Filter.Ne(ActiveSimField, true));

        var result = await _collection.DeleteOneAsync(deleteFilter, cancellationToken).ConfigureAwait(false);
        return result.DeletedCount > 0;
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
            {
                if (element.Value.IsString)
                    modules[element.Name] = element.Value.AsString;
            }
        }

        return new UserCodeBranch(ResolveBranchName(document),
                                  modules,
                                  document.TryGetValue(TimestampField, out var timestampValue) && timestampValue.IsNumeric
                                      ? DateTimeOffset.FromUnixTimeMilliseconds(timestampValue.ToInt64()).UtcDateTime : null,
                                  document.GetBooleanOrDefault(ActiveWorldField),
                                  document.GetBooleanOrDefault(ActiveSimField));
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

    private static string ResolveBranchName(BsonDocument document)
        => document.GetStringOrNull(BranchField) ?? DefaultBranchName;
}
