using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

namespace ScreepsDotNet.Storage.MongoRedis.Repositories;

public sealed class MongoUserCodeRepository(IMongoDatabaseProvider databaseProvider) : IUserCodeRepository
{
    private const string DefaultBranchName = "default";
    private const string DefaultModuleName = "main";
    private const string ActivePrefix = "$";
    private const string ActiveWorldIdentifier = "$activeWorld";
    private const string ActiveSimIdentifier = "$activeSim";
    private const int MaxBranchCount = 30;

    private readonly IMongoCollection<UserCodeDocument> _collection = databaseProvider.GetCollection<UserCodeDocument>(databaseProvider.Settings.UserCodeCollection);

    public async Task<IReadOnlyCollection<UserCodeBranch>> GetBranchesAsync(string userId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<UserCodeDocument>.Filter.Eq(document => document.UserId, userId);
        var branches = await _collection.Find(filter)
                                        .ToListAsync(cancellationToken)
                                        .ConfigureAwait(false);

        if (branches.All(document => !string.Equals(ResolveBranchName(document), DefaultBranchName, StringComparison.Ordinal))) {
            await CreateDefaultBranchAsync(userId, cancellationToken).ConfigureAwait(false);
            branches = await _collection.Find(filter).ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        return branches.Select(ToBranch).ToList();
    }

    public async Task<UserCodeBranch?> GetBranchAsync(string userId, string branchIdentifier, CancellationToken cancellationToken = default)
    {
        var filter = BuildBranchFilter(userId, branchIdentifier);
        if (filter is null)
            return null;

        var document = await _collection.Find(filter)
                                         .FirstOrDefaultAsync(cancellationToken)
                                         .ConfigureAwait(false);
        return document is null ? null : ToBranch(document);
    }

    public async Task<bool> UpdateBranchModulesAsync(string userId, string branchIdentifier, IDictionary<string, string> modules, CancellationToken cancellationToken = default)
    {
        var filter = BuildBranchFilter(userId, branchIdentifier);
        if (filter is null)
            return false;

        var update = Builders<UserCodeDocument>.Update
            .Set(document => document.Modules, new Dictionary<string, string>(modules, StringComparer.Ordinal))
            .Set(document => document.Timestamp, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> SetActiveBranchAsync(string userId, string branchName, string activeName, CancellationToken cancellationToken = default)
    {
        var targetField = activeName.Equals("activeWorld", StringComparison.OrdinalIgnoreCase)
            ? nameof(UserCodeDocument.ActiveWorld)
            : activeName.Equals("activeSim", StringComparison.OrdinalIgnoreCase)
                ? nameof(UserCodeDocument.ActiveSim)
                : null;

        if (targetField is null)
            return false;

        var userFilter = Builders<UserCodeDocument>.Filter.Eq(document => document.UserId, userId);
        var branchFilter = Builders<UserCodeDocument>.Filter.And(userFilter, Builders<UserCodeDocument>.Filter.Eq(document => document.Branch, branchName));

        var update = Builders<UserCodeDocument>.Update
            .Set(targetField, true)
            .Set(document => document.Timestamp, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var result = await _collection.UpdateOneAsync(branchFilter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result.ModifiedCount == 0)
            return false;

        var resetFilter = Builders<UserCodeDocument>.Filter.And(userFilter, Builders<UserCodeDocument>.Filter.Ne(document => document.Branch, branchName));
        var resetUpdate = Builders<UserCodeDocument>.Update.Set(targetField, false);
        await _collection.UpdateManyAsync(resetFilter, resetUpdate, cancellationToken: cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> CloneBranchAsync(string userId, string? sourceBranch, string newBranchName, IDictionary<string, string>? defaultModules, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newBranchName) || newBranchName.Length > 30)
            return false;

        var userFilter = Builders<UserCodeDocument>.Filter.Eq(document => document.UserId, userId);
        var existingFilter = Builders<UserCodeDocument>.Filter.And(userFilter, Builders<UserCodeDocument>.Filter.Eq(document => document.Branch, newBranchName));
        var existing = await _collection.Find(existingFilter).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (existing is not null)
            return false;

        var branchCount = await _collection.CountDocumentsAsync(userFilter, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (branchCount >= MaxBranchCount)
            return false;

        IReadOnlyDictionary<string, string>? modules = null;
        if (!string.IsNullOrWhiteSpace(sourceBranch)) {
            var source = await GetBranchAsync(userId, sourceBranch, cancellationToken).ConfigureAwait(false);
            modules = source?.Modules;
        }

        modules ??= defaultModules is not null
            ? new Dictionary<string, string>(defaultModules, StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal) { [DefaultModuleName] = string.Empty };

        var document = new UserCodeDocument
        {
            UserId = userId,
            Branch = newBranchName,
            Modules = new Dictionary<string, string>(modules, StringComparer.Ordinal),
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ActiveWorld = false,
            ActiveSim = false
        };

        await _collection.InsertOneAsync(document, cancellationToken: cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> DeleteBranchAsync(string userId, string branchName, CancellationToken cancellationToken = default)
    {
        var userFilter = Builders<UserCodeDocument>.Filter.Eq(document => document.UserId, userId);
        var deleteFilter = Builders<UserCodeDocument>.Filter.And(
            userFilter,
            Builders<UserCodeDocument>.Filter.Eq(document => document.Branch, branchName),
            Builders<UserCodeDocument>.Filter.Ne(document => document.ActiveWorld, true),
            Builders<UserCodeDocument>.Filter.Ne(document => document.ActiveSim, true));

        var result = await _collection.DeleteOneAsync(deleteFilter, cancellationToken).ConfigureAwait(false);
        return result.DeletedCount > 0;
    }

    private static FilterDefinition<UserCodeDocument>? BuildBranchFilter(string userId, string branchIdentifier)
    {
        var userFilter = Builders<UserCodeDocument>.Filter.Eq(document => document.UserId, userId);

        if (string.IsNullOrWhiteSpace(branchIdentifier))
            return Builders<UserCodeDocument>.Filter.And(userFilter, Builders<UserCodeDocument>.Filter.Eq(document => document.Branch, DefaultBranchName));

        if (branchIdentifier.StartsWith(ActivePrefix, StringComparison.Ordinal)) {
            if (branchIdentifier.Equals(ActiveWorldIdentifier, StringComparison.OrdinalIgnoreCase))
                return Builders<UserCodeDocument>.Filter.And(userFilter, Builders<UserCodeDocument>.Filter.Eq(document => document.ActiveWorld, true));

            if (branchIdentifier.Equals(ActiveSimIdentifier, StringComparison.OrdinalIgnoreCase))
                return Builders<UserCodeDocument>.Filter.And(userFilter, Builders<UserCodeDocument>.Filter.Eq(document => document.ActiveSim, true));

            return null;
        }

        return Builders<UserCodeDocument>.Filter.And(userFilter, Builders<UserCodeDocument>.Filter.Eq(document => document.Branch, branchIdentifier));
    }

    private static UserCodeBranch ToBranch(UserCodeDocument document)
    {
        var timestamp = document.Timestamp.HasValue
            ? DateTimeOffset.FromUnixTimeMilliseconds(document.Timestamp.Value).UtcDateTime
            : (DateTime?)null;

        var modules = document.Modules is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(document.Modules, StringComparer.Ordinal);

        return new UserCodeBranch(ResolveBranchName(document), modules, timestamp, document.ActiveWorld ?? false, document.ActiveSim ?? false);
    }

    private Task CreateDefaultBranchAsync(string userId, CancellationToken cancellationToken)
    {
        var document = new UserCodeDocument
        {
            UserId = userId,
            Branch = DefaultBranchName,
            Modules = new Dictionary<string, string>(StringComparer.Ordinal) { [DefaultModuleName] = string.Empty },
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ActiveWorld = true,
            ActiveSim = false
        };

        return _collection.InsertOneAsync(document, cancellationToken: cancellationToken);
    }

    private static string ResolveBranchName(UserCodeDocument document)
        => string.IsNullOrWhiteSpace(document.Branch) ? DefaultBranchName : document.Branch;
}
