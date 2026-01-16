namespace ScreepsDotNet.Storage.MongoRedis.Services;

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Backend.Core.Models.Bots;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

public sealed class MongoBotControlService(IMongoDatabaseProvider databaseProvider, IBotDefinitionProvider botDefinitionProvider, IUserMemoryRepository userMemoryRepository,
                                           IUserRespawnService userRespawnService, IWorldMetadataRepository worldMetadata, ILogger<MongoBotControlService> logger)
    : IBotControlService
{
    private const int DefaultCpuLimit = 100;
    private const int DefaultSafeModeDuration = 20_000;
    private const int DefaultInvaderGoal = 1_000_000;

    private readonly IMongoCollection<UserDocument> _usersCollection = databaseProvider.GetCollection<UserDocument>(databaseProvider.Settings.UsersCollection);
    private readonly IMongoCollection<UserCodeDocument> _userCodeCollection = databaseProvider.GetCollection<UserCodeDocument>(databaseProvider.Settings.UserCodeCollection);
    private readonly IMongoCollection<UserMemoryDocument> _userMemoryCollection = databaseProvider.GetCollection<UserMemoryDocument>(databaseProvider.Settings.UserMemoryCollection);
    private readonly IMongoCollection<RoomDocument> _roomsCollection = databaseProvider.GetCollection<RoomDocument>(databaseProvider.Settings.RoomsCollection);
    private readonly IMongoCollection<RoomObjectDocument> _roomObjectsCollection = databaseProvider.GetCollection<RoomObjectDocument>(databaseProvider.Settings.RoomObjectsCollection);
    private readonly IMongoCollection<RoomTerrainDocument> _roomTerrainCollection = databaseProvider.GetCollection<RoomTerrainDocument>(databaseProvider.Settings.RoomTerrainCollection);
    private readonly Random _random = new();

    public async Task<BotSpawnResult> SpawnAsync(string botName, string roomName, string? shardName, BotSpawnOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(botName);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);
        var normalizedRoom = roomName.Trim();
        var normalizedShard = string.IsNullOrWhiteSpace(shardName) ? null : shardName.Trim();

        var definition = await botDefinitionProvider.FindDefinitionAsync(botName, cancellationToken).ConfigureAwait(false)
                         ?? throw new InvalidOperationException($"Bot '{botName}' is not defined in mods.json.");

        if (definition.Modules.Count == 0)
            throw new InvalidOperationException($"Bot '{botName}' does not contain any modules.");

        var controllerFilterBuilder = Builders<RoomObjectDocument>.Filter;
        var controllerFilter = BuildRoomObjectRoomFilter(normalizedRoom, normalizedShard) &
                               controllerFilterBuilder.Eq(doc => doc.Type, StructureType.Controller.ToDocumentValue());

        var controller = await _roomObjectsCollection.Find(controllerFilter)
                                                     .FirstOrDefaultAsync(cancellationToken)
                                                     .ConfigureAwait(false) ?? throw new InvalidOperationException($"Room {normalizedRoom} does not contain a controller.");
        if (controller.UserId is not null)
            throw new InvalidOperationException($"Room {normalizedRoom} is already owned by {controller.UserId}.");

        var username = string.IsNullOrWhiteSpace(options.Username)
            ? await GenerateUniqueUsernameAsync(cancellationToken).ConfigureAwait(false)
            : await EnsureUsernameAvailableAsync(options.Username, cancellationToken).ConfigureAwait(false);

        var userId = ObjectId.GenerateNewId().ToString();
        var userDocument = new UserDocument
        {
            Id = userId,
            Username = username,
            UsernameLower = username.ToLowerInvariant(),
            Cpu = options.Cpu ?? DefaultCpuLimit,
            Active = 1,
            Bot = botName,
            Badge = GenerateRandomBadge(),
            LastRespawnDate = DateTime.UtcNow,
            Gcl = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [UserDocumentFields.GclFields.Level] = options.GlobalControlLevel ?? 1,
                [UserDocumentFields.GclFields.Progress] = 0,
                [UserDocumentFields.GclFields.ProgressTotal] = 0
            }
        };

        await _usersCollection.InsertOneAsync(userDocument, cancellationToken: cancellationToken).ConfigureAwait(false);

        var branchId = $"bot-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        var codeDocument = new UserCodeDocument
        {
            Id = ObjectId.GenerateNewId(),
            UserId = userId,
            Branch = branchId,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Modules = new Dictionary<string, string>(definition.Modules, StringComparer.Ordinal),
            ActiveWorld = true,
            ActiveSim = true
        };

        await _userCodeCollection.InsertOneAsync(codeDocument, cancellationToken: cancellationToken).ConfigureAwait(false);

        using (var memoryPayload = JsonDocument.Parse("{}"))
            await userMemoryRepository.UpdateMemoryAsync(userId, null, memoryPayload.RootElement, cancellationToken).ConfigureAwait(false);

        var terrainFilter = BuildTerrainFilter(normalizedRoom, normalizedShard);
        var roomTerrain = await _roomTerrainCollection.Find(terrainFilter)
                                                      .FirstOrDefaultAsync(cancellationToken)
                                                      .ConfigureAwait(false)
                           ?? throw new InvalidOperationException($"Terrain data for room {normalizedRoom} was not found.");

        var roomObjectsFilter = BuildRoomObjectRoomFilter(normalizedRoom, normalizedShard);
        var roomObjects = await _roomObjectsCollection.Find(roomObjectsFilter)
                                                      .ToListAsync(cancellationToken)
                                                      .ConfigureAwait(false);

        var (spawnX, spawnY) = ResolveSpawnPosition(roomTerrain.Terrain, roomObjects, options);
        await InsertSpawnAsync(normalizedRoom, normalizedShard, userId, spawnX, spawnY, cancellationToken).ConfigureAwait(false);

        var gameTime = await worldMetadata.GetGameTimeAsync(cancellationToken).ConfigureAwait(false);
        var safeModeExpiry = gameTime + DefaultSafeModeDuration;
        var controllerUpdate = Builders<RoomObjectDocument>.Update
                                                     .Set(doc => doc.UserId, userId)
                                                     .Set(doc => doc.Level, options.GlobalControlLevel ?? 1)
                                                     .Set(doc => doc.Progress, 0)
                                                     .Set(doc => doc.DowngradeTime, safeModeExpiry)
                                                     .Set(doc => doc.SafeMode, safeModeExpiry);

        await _roomObjectsCollection.UpdateOneAsync(controllerFilter,
                                                    controllerUpdate,
                                                    cancellationToken: cancellationToken)
                                    .ConfigureAwait(false);

        var roomFilter = BuildRoomDocumentFilter(normalizedRoom, normalizedShard);
        var roomUpdate = Builders<RoomDocument>.Update
                                               .Set(doc => doc.Status, "normal")
                                               .Set(doc => doc.InvaderGoal, DefaultInvaderGoal)
                                               .Set(doc => doc.Shard, normalizedShard);

        await _roomsCollection.UpdateOneAsync(roomFilter,
                                              roomUpdate,
                                              new UpdateOptions { IsUpsert = true },
                                              cancellationToken)
                              .ConfigureAwait(false);

        logger.LogInformation("Spawned bot {Username} ({Bot}) in {Room}.", username, botName, roomName);
        return new BotSpawnResult(userId, username, normalizedRoom, normalizedShard, spawnX, spawnY);
    }

    public async Task<int> ReloadAsync(string botName, CancellationToken cancellationToken = default)
    {
        var definition = await botDefinitionProvider.FindDefinitionAsync(botName, cancellationToken).ConfigureAwait(false)
                         ?? throw new InvalidOperationException($"Bot '{botName}' is not defined.");

        var users = await _usersCollection.Find(user => user.Bot == botName)
                                          .ToListAsync(cancellationToken)
                                          .ConfigureAwait(false);

        if (users.Count == 0)
            return 0;

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var branchId = $"bot-{timestamp}";

        var newDocuments = users.Select(user => new UserCodeDocument
        {
            Id = ObjectId.GenerateNewId(),
            UserId = user.Id!,
            Branch = branchId,
            Timestamp = timestamp,
            Modules = new Dictionary<string, string>(definition.Modules, StringComparer.Ordinal),
            ActiveWorld = true,
            ActiveSim = true
        }).ToList();

        if (newDocuments.Count > 0)
            await _userCodeCollection.InsertManyAsync(newDocuments, cancellationToken: cancellationToken).ConfigureAwait(false);

        foreach (var user in users) {
            var filter = Builders<UserCodeDocument>.Filter.And(
                Builders<UserCodeDocument>.Filter.Eq(doc => doc.UserId, user.Id),
                Builders<UserCodeDocument>.Filter.Ne(doc => doc.Branch, branchId));
            await _userCodeCollection.DeleteManyAsync(filter, cancellationToken).ConfigureAwait(false);
        }

        logger.LogInformation("Reloaded bot AI '{Bot}' for {Count} users.", botName, users.Count);
        return users.Count;
    }

    public async Task<bool> RemoveAsync(string username, CancellationToken cancellationToken = default)
    {
        var usernameLower = username.ToLowerInvariant();
        var user = await _usersCollection.Find(document => document.UsernameLower == usernameLower)
                                         .FirstOrDefaultAsync(cancellationToken)
                                         .ConfigureAwait(false);

        if (user?.Id is null)
            return false;

        if (string.IsNullOrWhiteSpace(user.Bot))
            throw new InvalidOperationException($"User '{username}' is not a bot account.");

        await userRespawnService.RespawnAsync(user.Id, cancellationToken).ConfigureAwait(false);
        await _usersCollection.DeleteOneAsync(document => document.Id == user.Id, cancellationToken).ConfigureAwait(false);
        await _userCodeCollection.DeleteManyAsync(document => document.UserId == user.Id, cancellationToken).ConfigureAwait(false);
        await _userMemoryCollection.DeleteOneAsync(document => document.UserId == user.Id, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Removed bot user {Username}.", username);
        return true;
    }

    private async Task<string> GenerateUniqueUsernameAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++) {
            var baseName = RandomNames[_random.Next(RandomNames.Length)];
            var candidate = attempt == 0 ? $"{baseName}Bot" : $"{baseName}Bot{_random.Next(0, 1000).ToString(CultureInfo.InvariantCulture)}";
            var exists = await _usersCollection.Find(user => user.UsernameLower == candidate.ToLowerInvariant())
                                               .AnyAsync(cancellationToken)
                                               .ConfigureAwait(false);
            if (!exists)
                return candidate;
        }

        throw new InvalidOperationException("Unable to generate a unique bot username.");
    }

    private async Task<string> EnsureUsernameAvailableAsync(string requestedUsername, CancellationToken cancellationToken)
    {
        var lower = requestedUsername.ToLowerInvariant();
        var exists = await _usersCollection.Find(user => user.UsernameLower == lower)
                                           .AnyAsync(cancellationToken)
                                           .ConfigureAwait(false);
        if (exists)
            throw new InvalidOperationException($"User '{requestedUsername}' already exists.");

        return requestedUsername;
    }

    private static Dictionary<string, object?> GenerateRandomBadge()
    {
        static string RandomColor(Random random) => $"#{random.Next(0x000000, 0xFFFFFF):X6}";

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [UserDocumentFields.BadgeFields.Type] = 1,
            [UserDocumentFields.BadgeFields.Color1] = RandomColor(Random.Shared),
            [UserDocumentFields.BadgeFields.Color2] = RandomColor(Random.Shared),
            [UserDocumentFields.BadgeFields.Color3] = RandomColor(Random.Shared),
            [UserDocumentFields.BadgeFields.Flip] = Random.Shared.NextDouble() >= 0.5,
            [UserDocumentFields.BadgeFields.Param] = Random.Shared.Next(-100, 101)
        };
    }

    private (int x, int y) ResolveSpawnPosition(string? terrain, IReadOnlyCollection<RoomObjectDocument> roomObjects, BotSpawnOptions options)
    {
        if (terrain is null)
            throw new InvalidOperationException("Room terrain is missing.");

        if (options.SpawnX is not null && options.SpawnY is not null) {
            var x = options.SpawnX.Value;
            var y = options.SpawnY.Value;
            ValidateCoordinate(x);
            ValidateCoordinate(y);

            if (IsWall(terrain, x, y))
                throw new InvalidOperationException($"Cannot place spawn at ({x},{y}); tile is a wall.");

            if (IsOccupied(roomObjects, x, y))
                throw new InvalidOperationException($"Cannot place spawn at ({x},{y}); tile already occupied.");

            return (x, y);
        }

        for (var attempt = 0; attempt < 200; attempt++) {
            var x = _random.Next(3, 47);
            var y = _random.Next(3, 47);
            if (IsWall(terrain, x, y) || IsOccupied(roomObjects, x, y))
                continue;

            return (x, y);
        }

        throw new InvalidOperationException("Unable to find a free tile for the spawn.");
    }

    private async Task InsertSpawnAsync(string roomName, string? shardName, string userId, int x, int y, CancellationToken cancellationToken)
    {
        var spawnDoc = new RoomObjectDocument
        {
            Id = ObjectId.GenerateNewId(),
            Type = StructureType.Spawn.ToDocumentValue(),
            Room = roomName,
            Shard = shardName,
            X = x,
            Y = y,
            Name = "Spawn1",
            UserId = userId,
            Store = new Dictionary<string, int> { [RoomDocumentFields.RoomObject.Store.Energy] = ScreepsGameConstants.SpawnInitialEnergy },
            StoreCapacityResource = new Dictionary<string, int> { [RoomDocumentFields.RoomObject.Store.Energy] = ScreepsGameConstants.SpawnEnergyCapacity },
            Hits = ScreepsGameConstants.SpawnHits,
            HitsMax = ScreepsGameConstants.SpawnHits,
            Spawning = BsonNull.Value,
            NotifyWhenAttacked = false
        };

        await _roomObjectsCollection.InsertOneAsync(spawnDoc, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static FilterDefinition<RoomObjectDocument> BuildRoomObjectRoomFilter(string room, string? shard)
    {
        var builder = Builders<RoomObjectDocument>.Filter;
        var filter = builder.Eq(doc => doc.Room, room);
        if (!string.IsNullOrWhiteSpace(shard))
            return filter & builder.Eq(doc => doc.Shard, shard);

        return filter & builder.Or(builder.Eq(doc => doc.Shard, null),
                                   builder.Exists(nameof(RoomObjectDocument.Shard), false));
    }

    private static FilterDefinition<RoomTerrainDocument> BuildTerrainFilter(string room, string? shard)
    {
        var builder = Builders<RoomTerrainDocument>.Filter;
        var filter = builder.Eq(doc => doc.Room, room);
        if (!string.IsNullOrWhiteSpace(shard))
            return filter & builder.Eq(doc => doc.Shard, shard);

        return filter & builder.Or(builder.Eq(doc => doc.Shard, null),
                                   builder.Exists(nameof(RoomTerrainDocument.Shard), false));
    }

    private static FilterDefinition<RoomDocument> BuildRoomDocumentFilter(string room, string? shard)
    {
        var builder = Builders<RoomDocument>.Filter;
        var filter = builder.Eq(doc => doc.Id, room);
        if (!string.IsNullOrWhiteSpace(shard))
            return filter & builder.Eq(doc => doc.Shard, shard);

        return filter & builder.Or(builder.Eq(doc => doc.Shard, null),
                                   builder.Exists(nameof(RoomDocument.Shard), false));
    }

    private static void ValidateCoordinate(int coordinate)
    {
        if (coordinate is < 0 or > 49)
            throw new ArgumentOutOfRangeException(nameof(coordinate), "Coordinate must be between 0 and 49.");
    }

    private static bool IsWall(string terrain, int x, int y)
    {
        var index = (y * 50) + x;
        if (index < 0 || index >= terrain.Length)
            return true;

        var value = DecodeTerrainValue(terrain[index]);
        return (value & 1) != 0;
    }

    private static int DecodeTerrainValue(char value)
    {
        if (value is >= '0' and <= '9')
            return value - '0';
        if (value is >= 'a' and <= 'z')
            return 10 + (value - 'a');
        if (value is >= 'A' and <= 'Z')
            return 10 + (value - 'A');
        return 0;
    }

    private static bool IsOccupied(IReadOnlyCollection<RoomObjectDocument> objects, int x, int y)
        => objects.Any(obj => obj.X == x && obj.Y == y);

    private static readonly string[] RandomNames =
    [
        "Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot", "Nova", "Orion", "Vega", "Atlas",
        "Comet", "Lumen", "Titan", "Helix", "Aria", "Nimbus", "Quark", "Zephyr", "Lynx", "Phoenix"
    ];
}
