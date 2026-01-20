namespace ScreepsDotNet.Storage.MongoRedis.Services;

using System.Globalization;
using System.Linq;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

public sealed class MongoPowerCreepService : IPowerCreepService
{
    private const int MaxNameLength = 50;

    private readonly IMongoCollection<PowerCreepDocument> _powerCreeps;
    private readonly IMongoCollection<RoomObjectDocument> _roomObjects;
    private readonly IMongoCollection<UserDocument> _users;
    private readonly ILogger<MongoPowerCreepService> _logger;

    public MongoPowerCreepService(IMongoDatabaseProvider databaseProvider, ILogger<MongoPowerCreepService> logger)
    {
        ArgumentNullException.ThrowIfNull(databaseProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _powerCreeps = databaseProvider.GetCollection<PowerCreepDocument>(databaseProvider.Settings.UsersPowerCreepsCollection);
        _roomObjects = databaseProvider.GetCollection<RoomObjectDocument>(databaseProvider.Settings.RoomObjectsCollection);
        _users = databaseProvider.GetCollection<UserDocument>(databaseProvider.Settings.UsersCollection);
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<PowerCreepListItem>> GetListAsync(string userId, CancellationToken cancellationToken = default)
    {
        var creeps = await _powerCreeps.Find(creep => creep.UserId == userId)
                                       .ToListAsync(cancellationToken)
                                       .ConfigureAwait(false);

        if (creeps.Count == 0)
            return [];

        var creepIds = creeps.Select(creep => creep.Id).ToList();
        var roomObjects = await _roomObjects.Find(doc => creepIds.Contains(doc.Id))
                                            .ToListAsync(cancellationToken)
                                            .ConfigureAwait(false);
        var roomLookup = roomObjects.ToDictionary(doc => doc.Id, doc => doc, new ObjectIdEqualityComparer());

        var items = creeps.Select(creep => ToListItem(creep, roomLookup.TryGetValue(creep.Id, out var roomDoc) ? roomDoc : null))
                          .ToList();
        return items;
    }

    public async Task<PowerCreepListItem> CreateAsync(string userId, string name, string className, CancellationToken cancellationToken = default)
    {
        var (user, userPowerCreeps) = await LoadUserAndCreepsAsync(userId, cancellationToken).ConfigureAwait(false);
        EnsureClassValid(className);

        var trimmedName = NormalizeName(name);
        if (string.IsNullOrWhiteSpace(trimmedName))
            throw new PowerCreepValidationException("invalid name");

        if (userPowerCreeps.Any(creep => string.Equals(creep.Name, trimmedName, StringComparison.Ordinal)))
            throw new PowerCreepValidationException("name already exists");

        if (CalculateFreePowerLevels(user, userPowerCreeps) <= 0)
            throw new PowerCreepValidationException("not enough power level");

        var document = new PowerCreepDocument
        {
            Id = ObjectId.GenerateNewId(),
            UserId = userId,
            Name = trimmedName,
            ClassName = className,
            Level = 0,
            HitsMax = 1000,
            Store = new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity = 100,
            SpawnCooldownTime = 0,
            Powers = []
        };

        await _powerCreeps.InsertOneAsync(document, cancellationToken: cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("User {UserId} created power creep {Name} ({Id})", userId, trimmedName, document.Id.ToString());

        return ToListItem(document, null);
    }

    public async Task DeleteAsync(string userId, string creepId, CancellationToken cancellationToken = default)
    {
        var id = ParseObjectId(creepId);
        var creep = await _powerCreeps.Find(doc => doc.Id == id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (creep is null || !string.Equals(creep.UserId, userId, StringComparison.Ordinal))
            throw new PowerCreepValidationException("invalid id");

        if (creep.SpawnCooldownTime is null || !string.IsNullOrEmpty(creep.Shard))
            throw new PowerCreepValidationException("spawned");

        if (creep.DeleteTime.HasValue)
            throw new PowerCreepValidationException("already being deleted");

        var user = await _users.Find(document => document.Id == userId).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
                   ?? throw new PowerCreepValidationException("user not found");
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (user.PowerExperimentationTime.HasValue && user.PowerExperimentationTime.Value > now) {
            await _powerCreeps.DeleteOneAsync(doc => doc.Id == id, cancellationToken).ConfigureAwait(false);
            await _roomObjects.DeleteOneAsync(doc => doc.Id == id, cancellationToken: cancellationToken).ConfigureAwait(false);
            return;
        }

        var deleteTime = now + PowerConstants.PowerCreepDeleteCooldownMilliseconds;
        var update = Builders<PowerCreepDocument>.Update.Set(doc => doc.DeleteTime, deleteTime);
        await _powerCreeps.UpdateOneAsync(doc => doc.Id == id, update, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task CancelDeleteAsync(string userId, string creepId, CancellationToken cancellationToken = default)
    {
        var id = ParseObjectId(creepId);
        var creep = await _powerCreeps.Find(doc => doc.Id == id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (creep is null || !string.Equals(creep.UserId, userId, StringComparison.Ordinal))
            throw new PowerCreepValidationException("invalid id");

        if (!creep.DeleteTime.HasValue)
            throw new PowerCreepValidationException("not being deleted");

        var update = Builders<PowerCreepDocument>.Update.Unset(doc => doc.DeleteTime);
        await _powerCreeps.UpdateOneAsync(doc => doc.Id == id, update, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task RenameAsync(string userId, string creepId, string newName, CancellationToken cancellationToken = default)
    {
        var (user, creeps) = await LoadUserAndCreepsAsync(userId, cancellationToken).ConfigureAwait(false);
        _ = user; // user is currently unused but kept for future parity hooks.

        var id = ParseObjectId(creepId);
        var creep = creeps.FirstOrDefault(c => c.Id == id)
                    ?? throw new PowerCreepValidationException("invalid id");

        if (creep.SpawnCooldownTime is null || !string.IsNullOrEmpty(creep.Shard))
            throw new PowerCreepValidationException("spawned");

        var trimmed = NormalizeName(newName);
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new PowerCreepValidationException("invalid name");

        if (creeps.Any(c => c.Id != id && string.Equals(c.Name, trimmed, StringComparison.Ordinal)))
            throw new PowerCreepValidationException("name already exists");

        await _powerCreeps.UpdateOneAsync(doc => doc.Id == id,
                                          Builders<PowerCreepDocument>.Update.Set(doc => doc.Name, trimmed),
                                          cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task UpgradeAsync(string userId,
                                   string creepId,
                                   IReadOnlyDictionary<string, int> requestedPowers,
                                   CancellationToken cancellationToken = default)
    {
        if (requestedPowers is null || requestedPowers.Count == 0)
            throw new PowerCreepValidationException("invalid powers");

        var normalizedPowers = requestedPowers.ToDictionary(
            kvp => NormalizePowerKey(kvp.Key),
            kvp => kvp.Value,
            StringComparer.Ordinal);

        var (user, creeps) = await LoadUserAndCreepsAsync(userId, cancellationToken).ConfigureAwait(false);
        var id = ParseObjectId(creepId);
        var creep = creeps.FirstOrDefault(c => c.Id == id)
                    ?? throw new PowerCreepValidationException("invalid id");

        var newLevel = normalizedPowers.Values.Sum();
        if (newLevel > PowerConstants.PowerCreepMaxLevel)
            throw new PowerCreepValidationException("max level");

        var creepPowers = creep.Powers?.ToDictionary(kvp => ((int)kvp.Key).ToString(), kvp => kvp.Value.Level, StringComparer.Ordinal)
                          ?? new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var kvp in normalizedPowers) {
            if (!PowerConstants.PowerInfo.TryGetValue(kvp.Key, out var info))
                throw new PowerCreepValidationException($"invalid power {kvp.Key}");

            if (!string.Equals(info.ClassName, creep.ClassName, StringComparison.OrdinalIgnoreCase))
                throw new PowerCreepValidationException($"invalid class for power {kvp.Key}");

            var requestedLevel = kvp.Value;
            if (requestedLevel < 0)
                throw new PowerCreepValidationException($"invalid value for power {kvp.Key}");
            if (requestedLevel > PowerConstants.MaxPowerLevelPerAbility)
                throw new PowerCreepValidationException($"invalid max value for power {kvp.Key}");

            if (creepPowers.TryGetValue(kvp.Key, out var currentLevel) && requestedLevel < currentLevel)
                throw new PowerCreepValidationException($"cannot downgrade power {kvp.Key}");
        }

        foreach (var kvp in creepPowers) {
            var requestedLevel = normalizedPowers.TryGetValue(kvp.Key, out var value) ? value : 0;
            if (requestedLevel < kvp.Value)
                throw new PowerCreepValidationException($"cannot downgrade power {kvp.Key}");
        }

        foreach (var kvp in normalizedPowers) {
            if (kvp.Value == 0)
                continue;

            var powerInfo = PowerConstants.PowerInfo[kvp.Key];
            var levelRequirement = powerInfo.LevelRequirements[kvp.Value - 1];
            if (newLevel < levelRequirement)
                throw new PowerCreepValidationException($"not enough level for power {kvp.Key}");
        }

        if (!ArePowersValid(normalizedPowers))
            throw new PowerCreepValidationException("powers set is not valid");

        if (CalculateFreePowerLevels(user, creeps) < newLevel - (creep.Level ?? 0))
            throw new PowerCreepValidationException("not enough power level");

        var newStoreCapacity = 100 * (newLevel + 1);
        var newHitsMax = 1000 * (newLevel + 1);

        var update = Builders<PowerCreepDocument>.Update
                                                  .Set(doc => doc.Level, newLevel)
                                                  .Set(doc => doc.HitsMax, newHitsMax)
                                                  .Set(doc => doc.StoreCapacity, newStoreCapacity)
                                                  .Set(doc => doc.Powers, BuildPowerDocuments(normalizedPowers));

        await _powerCreeps.UpdateOneAsync(doc => doc.Id == id, update, cancellationToken: cancellationToken).ConfigureAwait(false);
        await _roomObjects.UpdateOneAsync(doc => doc.Id == id,
                                          Builders<RoomObjectDocument>.Update
                                                                      .Set(doc => doc.Level, newLevel)
                                                                      .Set(doc => doc.HitsMax, newHitsMax)
                                                                      .Set(doc => doc.StoreCapacity, newStoreCapacity),
                                          cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task RegisterExperimentationAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _users.Find(document => document.Id == userId)
                               .FirstOrDefaultAsync(cancellationToken)
                               .ConfigureAwait(false)
                   ?? throw new PowerCreepValidationException("user not found");

        var resets = (int?)user.PowerExperimentations ?? 0;
        if (resets <= 0)
            throw new PowerCreepValidationException("no power resets");

        var update = Builders<UserDocument>.Update
                                           .Inc(doc => doc.PowerExperimentations, -1)
                                           .Set(doc => doc.PowerExperimentationTime,
                                                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + PowerConstants.PowerExperimentationCooldownMilliseconds);
        await _users.UpdateOneAsync(doc => doc.Id == userId, update, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task<(UserDocument User, List<PowerCreepDocument> Creeps)> LoadUserAndCreepsAsync(string userId, CancellationToken cancellationToken)
    {
        var userTask = _users.Find(document => document.Id == userId).FirstOrDefaultAsync(cancellationToken);
        var creepsTask = _powerCreeps.Find(doc => doc.UserId == userId).ToListAsync(cancellationToken);
        await Task.WhenAll(userTask, creepsTask).ConfigureAwait(false);

        var user = await userTask.ConfigureAwait(false);
        var creeps = await creepsTask.ConfigureAwait(false);

        if (user is null)
            throw new PowerCreepValidationException("user not found");

        return (user, creeps);
    }

    private static int CalculateFreePowerLevels(UserDocument user, IReadOnlyCollection<PowerCreepDocument> creeps)
    {
        var totalPower = user.Power ?? 0;
        var maxLevel = (int)Math.Floor(Math.Pow(totalPower / PowerConstants.PowerLevelMultiply, 1.0 / PowerConstants.PowerLevelPow));
        var used = creeps.Count + creeps.Sum(creep => creep.Level ?? 0);
        return maxLevel - used;
    }

    private static string NormalizeName(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        var normalizedName = trimmed.Length > MaxNameLength ? trimmed[..MaxNameLength] : trimmed;
        return normalizedName;
    }

    private static string NormalizePowerKey(string key)
        => int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value.ToString(CultureInfo.InvariantCulture)
            : key;

    private static void EnsureClassValid(string className)
    {
        if (!PowerConstants.Classes.All.Contains(className))
            throw new PowerCreepValidationException("invalid class");
    }

    private static ObjectId ParseObjectId(string id)
    {
        if (!ObjectId.TryParse(id, out var objectId))
            throw new PowerCreepValidationException("invalid id");
        return objectId;
    }

    private static PowerCreepListItem ToListItem(PowerCreepDocument document, RoomObjectDocument? roomDoc)
    {
        var store = roomDoc?.Store is not null
            ? new Dictionary<string, int>(roomDoc.Store, StringComparer.Ordinal)
            : document.Store is null
                ? new Dictionary<string, int>(StringComparer.Ordinal)
                : new Dictionary<string, int>(document.Store, StringComparer.Ordinal);

        var powers = document.Powers is null
            ? new Dictionary<string, int>(StringComparer.Ordinal)
            : document.Powers.ToDictionary(kvp => ((int)kvp.Key).ToString(), kvp => kvp.Value.Level, StringComparer.Ordinal);

        var storeCapacity = roomDoc?.StoreCapacity ?? document.StoreCapacity ?? 0;
        var hitsMax = roomDoc?.HitsMax ?? document.HitsMax ?? 0;

        var shard = roomDoc?.Shard ?? document.Shard;

        return new PowerCreepListItem(
            document.Id.ToString(),
            document.Name,
            document.ClassName ?? PowerConstants.Classes.Operator,
            document.Level ?? 0,
            hitsMax,
            store,
            storeCapacity,
            document.SpawnCooldownTime,
            document.DeleteTime,
            shard,
            powers,
            roomDoc?.Room,
            roomDoc?.X,
            roomDoc?.Y,
            roomDoc?.Hits,
            roomDoc?.Fatigue,
            roomDoc?.TicksToLive);
    }

    private static bool ArePowersValid(IReadOnlyDictionary<string, int> requested)
    {
        var totalDesiredLevel = requested.Values.Sum();
        var recreated = PowerConstants.PowerInfo.ToDictionary(kvp => kvp.Key, _ => 0, StringComparer.Ordinal);

        var currentLevel = 0;
        while (currentLevel < totalDesiredLevel) {
            var nextPower = requested.Keys
                                     .Select(NormalizePowerKey)
                                     .FirstOrDefault(powerKey =>
                                         recreated.TryGetValue(powerKey, out var currentPowerLevel) &&
                                         currentPowerLevel < PowerConstants.MaxPowerLevelPerAbility &&
                                         requested.TryGetValue(powerKey, out var desiredLevel) &&
                                         currentPowerLevel < desiredLevel &&
                                         PowerConstants.PowerInfo[powerKey].LevelRequirements[currentPowerLevel] <= currentLevel);
            if (nextPower is null)
                return false;

            recreated[nextPower]++;
            currentLevel = recreated.Values.Sum();
        }

        return true;
    }

    private static Dictionary<PowerTypes, PowerCreepPowerDocument> BuildPowerDocuments(IReadOnlyDictionary<string, int> requestedPowers)
    {
        var result = new Dictionary<PowerTypes, PowerCreepPowerDocument>();
        foreach (var kvp in requestedPowers) {
            if (kvp.Value <= 0)
                continue;

            if (int.TryParse(NormalizePowerKey(kvp.Key), out var powerTypeInt))
                result[(PowerTypes)powerTypeInt] = new PowerCreepPowerDocument { Level = kvp.Value };
        }
        return result;
    }

    private sealed class ObjectIdEqualityComparer : IEqualityComparer<ObjectId>
    {
        public bool Equals(ObjectId x, ObjectId y) => x == y;

        public int GetHashCode(ObjectId obj) => obj.GetHashCode();
    }
}
