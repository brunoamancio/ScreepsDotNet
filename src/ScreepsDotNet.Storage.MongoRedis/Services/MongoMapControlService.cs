namespace ScreepsDotNet.Storage.MongoRedis.Services;

using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Backend.Core.Models.Map;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

public sealed partial class MongoMapControlService(IMongoDatabaseProvider databaseProvider, ILogger<MongoMapControlService> logger) : IMapControlService
{
    private static readonly string[] MineralPool = ["H", "O", "U", "L", "K", "Z", "X"];
    private static readonly char[] TerrainAlphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();

    private readonly IMongoCollection<RoomDocument> _rooms = databaseProvider.GetCollection<RoomDocument>(databaseProvider.Settings.RoomsCollection);
    private readonly IMongoCollection<RoomObjectDocument> _roomObjects = databaseProvider.GetCollection<RoomObjectDocument>(databaseProvider.Settings.RoomObjectsCollection);
    private readonly IMongoCollection<RoomTerrainDocument> _roomTerrain = databaseProvider.GetCollection<RoomTerrainDocument>(databaseProvider.Settings.RoomTerrainCollection);

    public async Task<MapGenerationResult> GenerateRoomAsync(MapRoomGenerationOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateRoomName(options.RoomName);
        var normalizedShard = string.IsNullOrWhiteSpace(options.ShardName) ? null : options.ShardName.Trim();

        var normalizedSources = Math.Clamp(options.SourceCount, 1, 3);
        var mineralType = string.IsNullOrWhiteSpace(options.MineralType)
            ? MineralPool[RandomNumberGenerator.GetInt32(MineralPool.Length)]
            : options.MineralType;

        var random = options.Seed.HasValue ? new Random(options.Seed.Value) : Random.Shared;

        var roomFilter = BuildRoomFilter(options.RoomName, normalizedShard);
        var existingRoom = await _rooms.Find(roomFilter)
                                       .FirstOrDefaultAsync(cancellationToken)
                                       .ConfigureAwait(false);
        if (existingRoom is not null && !options.OverwriteExisting)
            throw new InvalidOperationException($"Room {options.RoomName} already exists. Use --overwrite to replace it.");

        var roomDocument = new RoomDocument
        {
            Id = options.RoomName,
            Shard = normalizedShard,
            Status = RoomDocumentFields.RoomStatusValues.Normal,
            Novice = false,
            RespawnArea = false,
            OpenTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        await _rooms.ReplaceOneAsync(roomFilter,
                                     roomDocument,
                                     new ReplaceOptions { IsUpsert = true },
                                     cancellationToken).ConfigureAwait(false);

        var terrainString = CreateEncodedTerrain(options.TerrainPreset, random);
        await UpsertTerrainAsync(options.RoomName, normalizedShard, terrainString, cancellationToken).ConfigureAwait(false);

        await _roomObjects.DeleteManyAsync(BuildRoomObjectFilter(options.RoomName, normalizedShard), cancellationToken).ConfigureAwait(false);
        var newObjects = CreateRoomObjects(options, normalizedSources, mineralType, random);
        if (newObjects.Count > 0)
            await _roomObjects.InsertManyAsync(newObjects, cancellationToken: cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Generated room {Room} with {Objects} objects (controller: {Controller}, sources: {Sources}, keeperLairs: {Lairs}).",
            options.RoomName, newObjects.Count, options.IncludeController, normalizedSources, options.IncludeKeeperLairs);

        return new MapGenerationResult(options.RoomName,
                                       terrainString.Length,
                                       newObjects.Count,
                                       options.IncludeController,
                                       normalizedSources,
                                       options.IncludeKeeperLairs,
                                       mineralType);
    }

    public Task OpenRoomAsync(string roomName, string? shardName, CancellationToken cancellationToken = default)
        => SetRoomStatusAsync(roomName, shardName, RoomDocumentFields.RoomStatusValues.Normal, cancellationToken);

    public Task CloseRoomAsync(string roomName, string? shardName, CancellationToken cancellationToken = default)
        => SetRoomStatusAsync(roomName, shardName, RoomDocumentFields.RoomStatusValues.Closed, cancellationToken);

    public async Task RemoveRoomAsync(string roomName, string? shardName, bool purgeObjects, CancellationToken cancellationToken = default)
    {
        ValidateRoomName(roomName);

        var roomFilter = BuildRoomFilter(roomName, shardName);
        await _rooms.DeleteOneAsync(roomFilter, cancellationToken).ConfigureAwait(false);
        await _roomTerrain.DeleteOneAsync(BuildTerrainFilter(roomName, shardName), cancellationToken).ConfigureAwait(false);

        if (purgeObjects)
            await _roomObjects.DeleteManyAsync(BuildRoomObjectFilter(roomName, shardName), cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Removed room {Room} (purge objects: {Purge}).", roomName, purgeObjects);
    }

    public Task UpdateRoomAssetsAsync(string roomName, string? shardName, bool fullRegeneration, CancellationToken cancellationToken = default)
    {
        ValidateRoomName(roomName);
        var display = string.IsNullOrWhiteSpace(shardName) ? roomName : $"{shardName}/{roomName}";
        logger.LogWarning("Asset regeneration for room {Room} is not yet implemented. Skipping (full={Full}).", display, fullRegeneration);
        return Task.CompletedTask;
    }

    public async Task RefreshTerrainCacheAsync(CancellationToken cancellationToken = default)
    {
        var update = Builders<RoomTerrainDocument>.Update.Set(doc => doc.Type, "terrain");
        await _roomTerrain.UpdateManyAsync(FilterDefinition<RoomTerrainDocument>.Empty, update, cancellationToken: cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Refreshed terrain metadata for {Count} rooms.", await _roomTerrain.CountDocumentsAsync(FilterDefinition<RoomTerrainDocument>.Empty, cancellationToken: cancellationToken).ConfigureAwait(false));
    }

    private async Task SetRoomStatusAsync(string roomName, string? shardName, string status, CancellationToken cancellationToken)
    {
        ValidateRoomName(roomName);

        var update = Builders<RoomDocument>.Update
                                           .Set(room => room.Status, status)
                                           .Set(room => room.OpenTime, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var result = await _rooms.UpdateOneAsync(BuildRoomFilter(roomName, shardName), update, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result.MatchedCount == 0)
            throw new InvalidOperationException($"Room {roomName} does not exist.");

        logger.LogInformation("Room {Room} status set to {Status}.", roomName, status);
    }

    private static void ValidateRoomName(string roomName)
    {
        if (string.IsNullOrWhiteSpace(roomName) || !RoomNamePattern().IsMatch(roomName))
            throw new ArgumentException("Room name must match the Screeps notation (e.g., W8N3).", nameof(roomName));
    }

    private async Task UpsertTerrainAsync(string roomName, string? shardName, string terrainData, CancellationToken cancellationToken)
    {
        var filter = BuildTerrainFilter(roomName, shardName);
        var existing = await _roomTerrain.Find(filter)
                                         .FirstOrDefaultAsync(cancellationToken)
                                         .ConfigureAwait(false);

        var document = existing ?? new RoomTerrainDocument { Id = ObjectId.GenerateNewId(), Room = roomName, Shard = shardName };
        document.Shard = shardName;
        document.Type = "terrain";
        document.Terrain = terrainData;

        await _roomTerrain.ReplaceOneAsync(filter,
                                           document,
                                           new ReplaceOptions { IsUpsert = true },
                                           cancellationToken).ConfigureAwait(false);
    }

    private static List<RoomObjectDocument> CreateRoomObjects(MapRoomGenerationOptions options, int sourceCount, string mineralType, Random random)
    {
        var objects = new List<RoomObjectDocument>();

        if (options.IncludeController) {
            objects.Add(new RoomObjectDocument
            {
                Id = ObjectId.GenerateNewId(),
                Room = options.RoomName,
                Shard = options.ShardName,
                Type = RoomObjectType.Controller.ToDocumentValue(),
                Level = 1,
                X = 25,
                Y = 25
            });
        }

        for (var i = 0; i < sourceCount; i++) {
            var (x, y) = GetRandomInnerCoordinate(random);
            objects.Add(new RoomObjectDocument
            {
                Id = ObjectId.GenerateNewId(),
                Room = options.RoomName,
                Shard = options.ShardName,
                Type = StructureType.Source.ToDocumentValue(),
                X = x,
                Y = y
            });
        }

        objects.Add(new RoomObjectDocument
        {
            Id = ObjectId.GenerateNewId(),
            Room = options.RoomName,
            Shard = options.ShardName,
            Type = RoomObjectType.Mineral.ToDocumentValue(),
            MineralType = mineralType,
            Density = 3,
            X = 35,
            Y = 35
        });

        if (options.IncludeKeeperLairs) {
            foreach (var (x, y) in KeeperLairCoordinates) {
                objects.Add(new RoomObjectDocument
                {
                    Id = ObjectId.GenerateNewId(),
                    Room = options.RoomName,
                    Shard = options.ShardName,
                    Type = "keeperLair",
                    X = x,
                    Y = y
                });
            }
        }

        return objects;
    }

    private static readonly (int x, int y)[] KeeperLairCoordinates =
    [
        (10, 10),
        (10, 40),
        (40, 10),
        (40, 40)
    ];

    private static (int x, int y) GetRandomInnerCoordinate(Random random)
    {
        var x = random.Next(2, 48);
        var y = random.Next(2, 48);
        return (x, y);
    }

    private static string CreateEncodedTerrain(MapTerrainPreset preset, Random random)
    {
        Span<char> buffer = stackalloc char[50 * 50];
        for (var y = 0; y < 50; y++) {
            for (var x = 0; x < 50; x++) {
                var mask = ComputeTerrainMask(preset, x, y, random);
                buffer[(y * 50) + x] = EncodeMask(mask);
            }
        }

        return new string(buffer);
    }

    private static int ComputeTerrainMask(MapTerrainPreset preset, int x, int y, Random random)
    {
        var mask = 0;
        if (x == 0 || x == 49 || y == 0 || y == 49)
            mask |= 1; // walls around the border

        switch (preset) {
            case MapTerrainPreset.Plain:
                return mask;
            case MapTerrainPreset.SwampLow:
                if (random.NextDouble() < 0.1)
                    mask |= 2;
                break;
            case MapTerrainPreset.SwampHeavy:
                if (random.NextDouble() < 0.5)
                    mask |= 2;
                if (random.NextDouble() < 0.05)
                    mask |= 1;
                break;
            case MapTerrainPreset.Checker:
                if (((x / 3) + (y / 3)) % 2 == 0)
                    mask |= 2;
                break;
            case MapTerrainPreset.Mixed:
                if (random.NextDouble() < 0.2)
                    mask |= 2;
                if (random.NextDouble() < 0.03)
                    mask |= 1;
                break;
            default:
                break;
        }

        return mask;
    }

    private static char EncodeMask(int mask)
    {
        mask = Math.Clamp(mask, 0, TerrainAlphabet.Length - 1);
        return TerrainAlphabet[mask];
    }

    [GeneratedRegex("^[WE]\\d+[NS]\\d+$", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex RoomNamePattern();

    private static FilterDefinition<RoomDocument> BuildRoomFilter(string roomName, string? shardName)
    {
        var builder = Builders<RoomDocument>.Filter;
        var filter = builder.Eq(doc => doc.Id, roomName);
        if (!string.IsNullOrWhiteSpace(shardName))
            return filter & builder.Eq(doc => doc.Shard, shardName);

        return filter & builder.Or(builder.Eq(doc => doc.Shard, null),
                                   builder.Exists(nameof(RoomDocument.Shard), false));
    }

    private static FilterDefinition<RoomTerrainDocument> BuildTerrainFilter(string roomName, string? shardName)
    {
        var builder = Builders<RoomTerrainDocument>.Filter;
        var filter = builder.Eq(doc => doc.Room, roomName);
        if (!string.IsNullOrWhiteSpace(shardName))
            return filter & builder.Eq(doc => doc.Shard, shardName);

        return filter & builder.Or(builder.Eq(doc => doc.Shard, null),
                                   builder.Exists(nameof(RoomTerrainDocument.Shard), false));
    }

    private static FilterDefinition<RoomObjectDocument> BuildRoomObjectFilter(string roomName, string? shardName)
    {
        var builder = Builders<RoomObjectDocument>.Filter;
        var filter = builder.Eq(doc => doc.Room, roomName);
        if (!string.IsNullOrWhiteSpace(shardName))
            return filter & builder.Eq(doc => doc.Shard, shardName);

        return filter & builder.Or(builder.Eq(doc => doc.Shard, null),
                                   builder.Exists(nameof(RoomObjectDocument.Shard), false));
    }
}
