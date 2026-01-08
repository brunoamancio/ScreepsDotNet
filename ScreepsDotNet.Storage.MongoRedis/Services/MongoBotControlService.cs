namespace ScreepsDotNet.Storage.MongoRedis.Services;

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Models.Bots;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

public sealed class MongoBotControlService : IBotControlService
{
    private const int DefaultCpuLimit = 100;
    private const int DefaultSafeModeDuration = 20_000;
    private const int DefaultInvaderGoal = 1_000_000;
    private const int SpawnEnergyCapacity = 300;
    private const int SpawnEnergyStart = 300;
    private const int SpawnHits = 5_000;

    private readonly IBotDefinitionProvider _botDefinitionProvider;
    private readonly IUserMemoryRepository _userMemoryRepository;
    private readonly IUserRespawnService _userRespawnService;
    private readonly IWorldMetadataRepository _worldMetadata;
    private readonly ILogger<MongoBotControlService> _logger;
    private readonly IMongoCollection<UserDocument> _usersCollection;
    private readonly IMongoCollection<UserCodeDocument> _userCodeCollection;
    private readonly IMongoCollection<UserMemoryDocument> _userMemoryCollection;
    private readonly IMongoCollection<BsonDocument> _roomsCollection;
    private readonly IMongoCollection<BsonDocument> _roomObjectsCollection;
    private readonly IMongoCollection<RoomTerrainDocument> _roomTerrainCollection;
    private readonly Random _random = new();

    public MongoBotControlService(
        IMongoDatabaseProvider databaseProvider,
        IBotDefinitionProvider botDefinitionProvider,
        IUserMemoryRepository userMemoryRepository,
        IUserRespawnService userRespawnService,
        IWorldMetadataRepository worldMetadata,
        ILogger<MongoBotControlService> logger)
    {
        _botDefinitionProvider = botDefinitionProvider;
        _userMemoryRepository = userMemoryRepository;
        _userRespawnService = userRespawnService;
        _worldMetadata = worldMetadata;
        _logger = logger;

        _usersCollection = databaseProvider.GetCollection<UserDocument>(databaseProvider.Settings.UsersCollection);
        _userCodeCollection = databaseProvider.GetCollection<UserCodeDocument>(databaseProvider.Settings.UserCodeCollection);
        _userMemoryCollection = databaseProvider.GetCollection<UserMemoryDocument>(databaseProvider.Settings.UserMemoryCollection);
        _roomsCollection = databaseProvider.GetCollection<BsonDocument>(databaseProvider.Settings.RoomsCollection);
        _roomObjectsCollection = databaseProvider.GetCollection<BsonDocument>(databaseProvider.Settings.RoomObjectsCollection);
        _roomTerrainCollection = databaseProvider.GetCollection<RoomTerrainDocument>(databaseProvider.Settings.RoomTerrainCollection);
    }

    public async Task<BotSpawnResult> SpawnAsync(string botName, string roomName, BotSpawnOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(botName);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);

        var definition = await _botDefinitionProvider.FindDefinitionAsync(botName, cancellationToken).ConfigureAwait(false)
                         ?? throw new InvalidOperationException($"Bot '{botName}' is not defined in mods.json.");

        if (definition.Modules.Count == 0)
            throw new InvalidOperationException($"Bot '{botName}' does not contain any modules.");

        var controllerFilter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("room", roomName),
            Builders<BsonDocument>.Filter.Eq("type", "controller"));

        var controller = await _roomObjectsCollection.Find(controllerFilter)
                                                     .FirstOrDefaultAsync(cancellationToken)
                                                     .ConfigureAwait(false);
        if (controller is null)
            throw new InvalidOperationException($"Room {roomName} does not contain a controller.");

        if (controller.TryGetValue("user", out var existingOwner) && !existingOwner.IsBsonNull)
            throw new InvalidOperationException($"Room {roomName} is already owned by {existingOwner}.");

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
                ["level"] = options.GlobalControlLevel ?? 1,
                ["progress"] = 0,
                ["progressTotal"] = 0
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
            await _userMemoryRepository.UpdateMemoryAsync(userId, null, memoryPayload.RootElement, cancellationToken).ConfigureAwait(false);

        var roomTerrain = await _roomTerrainCollection.Find(document => document.Room == roomName)
                                                      .FirstOrDefaultAsync(cancellationToken)
                                                      .ConfigureAwait(false)
                           ?? throw new InvalidOperationException($"Terrain data for room {roomName} was not found.");

        var roomObjects = await _roomObjectsCollection.Find(Builders<BsonDocument>.Filter.Eq("room", roomName))
                                                      .ToListAsync(cancellationToken)
                                                      .ConfigureAwait(false);

        var (spawnX, spawnY) = ResolveSpawnPosition(roomTerrain.Terrain, roomObjects, options);
        await InsertSpawnAsync(roomName, userId, spawnX, spawnY, cancellationToken).ConfigureAwait(false);

        var gameTime = await _worldMetadata.GetGameTimeAsync(cancellationToken).ConfigureAwait(false);
        var safeModeExpiry = gameTime + DefaultSafeModeDuration;
        var controllerUpdate = Builders<BsonDocument>.Update
                                                     .Set("user", userId)
                                                     .Set("level", options.GlobalControlLevel ?? 1)
                                                     .Set("progress", 0)
                                                     .Set("downgradeTime", safeModeExpiry)
                                                     .Set("safeMode", safeModeExpiry);

        await _roomObjectsCollection.UpdateOneAsync(controllerFilter,
                                                    controllerUpdate,
                                                    cancellationToken: cancellationToken)
                                    .ConfigureAwait(false);

        var roomUpdate = Builders<BsonDocument>.Update
                                               .Set("status", "normal")
                                               .Set("invaderGoal", DefaultInvaderGoal);

        await _roomsCollection.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", roomName),
                                              roomUpdate,
                                              new UpdateOptions { IsUpsert = true },
                                              cancellationToken)
                              .ConfigureAwait(false);

        _logger.LogInformation("Spawned bot {Username} ({Bot}) in {Room}.", username, botName, roomName);
        return new BotSpawnResult(userId, username, roomName, spawnX, spawnY);
    }

    public async Task<int> ReloadAsync(string botName, CancellationToken cancellationToken = default)
    {
        var definition = await _botDefinitionProvider.FindDefinitionAsync(botName, cancellationToken).ConfigureAwait(false)
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

        _logger.LogInformation("Reloaded bot AI '{Bot}' for {Count} users.", botName, users.Count);
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

        await _userRespawnService.RespawnAsync(user.Id, cancellationToken).ConfigureAwait(false);
        await _usersCollection.DeleteOneAsync(document => document.Id == user.Id, cancellationToken).ConfigureAwait(false);
        await _userCodeCollection.DeleteManyAsync(document => document.UserId == user.Id, cancellationToken).ConfigureAwait(false);
        await _userMemoryCollection.DeleteOneAsync(document => document.UserId == user.Id, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Removed bot user {Username}.", username);
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
            ["type"] = 1,
            ["color1"] = RandomColor(Random.Shared),
            ["color2"] = RandomColor(Random.Shared),
            ["color3"] = RandomColor(Random.Shared),
            ["flip"] = Random.Shared.NextDouble() >= 0.5,
            ["param"] = Random.Shared.Next(-100, 101)
        };
    }

    private (int x, int y) ResolveSpawnPosition(string? terrain, IReadOnlyCollection<BsonDocument> roomObjects, BotSpawnOptions options)
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

    private async Task InsertSpawnAsync(string roomName, string userId, int x, int y, CancellationToken cancellationToken)
    {
        var spawnDoc = new BsonDocument
        {
            ["_id"] = ObjectId.GenerateNewId(),
            ["type"] = "spawn",
            ["room"] = roomName,
            ["x"] = x,
            ["y"] = y,
            ["name"] = "Spawn1",
            ["user"] = userId,
            ["store"] = new BsonDocument("energy", SpawnEnergyStart),
            ["storeCapacityResource"] = new BsonDocument("energy", SpawnEnergyCapacity),
            ["hits"] = SpawnHits,
            ["hitsMax"] = SpawnHits,
            ["spawning"] = BsonNull.Value,
            ["notifyWhenAttacked"] = false
        };

        await _roomObjectsCollection.InsertOneAsync(spawnDoc, cancellationToken: cancellationToken).ConfigureAwait(false);
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

    private static bool IsOccupied(IReadOnlyCollection<BsonDocument> objects, int x, int y)
        => objects.Any(obj => obj.TryGetValue("x", out var ox)
                              && obj.TryGetValue("y", out var oy)
                              && ox.IsInt32 && oy.IsInt32
                              && ox.AsInt32 == x
                              && oy.AsInt32 == y);

    private static readonly string[] RandomNames =
    [
        "Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot", "Nova", "Orion", "Vega", "Atlas",
        "Comet", "Lumen", "Titan", "Helix", "Aria", "Nimbus", "Quark", "Zephyr", "Lynx", "Phoenix"
    ];
}
