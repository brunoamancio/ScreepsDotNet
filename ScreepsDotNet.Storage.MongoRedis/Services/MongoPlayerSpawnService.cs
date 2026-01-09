namespace ScreepsDotNet.Storage.MongoRedis.Services;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

public sealed class MongoPlayerSpawnService(IMongoDatabaseProvider databaseProvider, IUserRepository userRepository, IWorldMetadataRepository worldMetadata, ILogger<MongoPlayerSpawnService> logger)
    : IPlayerSpawnService
{
    private const int SpawnEnergyCapacity = 300;
    private const int SpawnEnergyStart = 300;
    private const int SpawnHits = 5000;
    private const int DefaultSafeModeDuration = 20_000;
    private const int DefaultInvaderGoal = 1_000_000;

    private static readonly string[] CleanupObjectTypes =
    [
        StructureType.Creep.ToDocumentValue(),
        StructureType.ConstructionSite.ToDocumentValue()
    ];

    private readonly IMongoCollection<UserDocument> _usersCollection = databaseProvider.GetCollection<UserDocument>(databaseProvider.Settings.UsersCollection);
    private readonly IMongoCollection<BsonDocument> _roomsCollection = databaseProvider.GetCollection<BsonDocument>(databaseProvider.Settings.RoomsCollection);
    private readonly IMongoCollection<BsonDocument> _roomObjectsCollection = databaseProvider.GetCollection<BsonDocument>(databaseProvider.Settings.RoomObjectsCollection);
    private readonly IMongoCollection<RoomTerrainDocument> _roomTerrainCollection = databaseProvider.GetCollection<RoomTerrainDocument>(databaseProvider.Settings.RoomTerrainCollection);

    public async Task<PlaceSpawnResult> PlaceSpawnAsync(string userId, PlaceSpawnRequest request, CancellationToken cancellationToken = default)
    {
        if (request.X < 0 || request.X > 49 || request.Y < 0 || request.Y > 49)
            return new PlaceSpawnResult(PlaceSpawnResultStatus.InvalidParams, "Invalid coordinates");

        if (request.Name is { Length: > 50 })
            return new PlaceSpawnResult(PlaceSpawnResultStatus.InvalidParams, "Name too long");

        if (string.IsNullOrWhiteSpace(request.Room))
            return new PlaceSpawnResult(PlaceSpawnResultStatus.InvalidParams, "Invalid room");

        var roomName = request.Room.Trim();
        var shardName = string.IsNullOrWhiteSpace(request.Shard) ? null : request.Shard.Trim();
        var bsonFilterBuilder = Builders<BsonDocument>.Filter;

        var profile = await userRepository.GetProfileAsync(userId, cancellationToken).ConfigureAwait(false);
        if (profile == null)
            return new PlaceSpawnResult(PlaceSpawnResultStatus.UserNotFound);

        if (profile.Blocked)
            return new PlaceSpawnResult(PlaceSpawnResultStatus.Blocked);

        if (profile.Cpu <= 0)
            return new PlaceSpawnResult(PlaceSpawnResultStatus.NoCpu);

        if (profile.LastRespawnDate.HasValue && (DateTime.UtcNow - profile.LastRespawnDate.Value).TotalSeconds < 180)
            return new PlaceSpawnResult(PlaceSpawnResultStatus.TooSoonAfterLastRespawn);

        // Check if already playing
        var objectsCount = await _roomObjectsCollection.CountDocumentsAsync(
            Builders<BsonDocument>.Filter.Eq("user", userId),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (objectsCount > 0)
            return new PlaceSpawnResult(PlaceSpawnResultStatus.AlreadyPlaying);

        // Validate room and controller
        var controllerFilter = ShardFilterBuilder.ForRoom(bsonFilterBuilder, roomName, shardName) &
                               bsonFilterBuilder.Eq("type", StructureType.Controller.ToDocumentValue());
        var controller = await _roomObjectsCollection.Find(controllerFilter).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (controller == null)
            return new PlaceSpawnResult(PlaceSpawnResultStatus.InvalidRoom, "Room has no controller");

        if (controller.Contains("user") && !controller["user"].IsBsonNull)
            return new PlaceSpawnResult(PlaceSpawnResultStatus.InvalidRoom, "Room is already owned");

        // Validate position
        var terrainFilter = BuildTerrainFilter(roomName, shardName);
        var terrainDoc = await _roomTerrainCollection.Find(terrainFilter)
                                                     .FirstOrDefaultAsync(cancellationToken)
                                                     .ConfigureAwait(false);
        if (terrainDoc == null)
            return new PlaceSpawnResult(PlaceSpawnResultStatus.InvalidRoom, "Room terrain not found");

        if (IsWall(terrainDoc.Terrain, request.X, request.Y))
            return new PlaceSpawnResult(PlaceSpawnResultStatus.InvalidPosition, "Cannot place on wall");

        var positionFilter = ShardFilterBuilder.ForRoom(bsonFilterBuilder, roomName, shardName) &
                             bsonFilterBuilder.Eq("x", request.X) &
                             bsonFilterBuilder.Eq("y", request.Y);
        var objectsAtPos = await _roomObjectsCollection.Find(positionFilter)
                                                       .AnyAsync(cancellationToken)
                                                       .ConfigureAwait(false);
        if (objectsAtPos)
            return new PlaceSpawnResult(PlaceSpawnResultStatus.InvalidPosition, "Position is occupied");

        var gameTime = await worldMetadata.GetGameTimeAsync(cancellationToken).ConfigureAwait(false);

        // Room cleanup
        await CleanupRoomAsync(roomName, shardName, gameTime, cancellationToken).ConfigureAwait(false);

        // Update controller
        var safeModeExpiry = gameTime + DefaultSafeModeDuration;
        var controllerUpdate = Builders<BsonDocument>.Update
            .Set("user", userId)
            .Set("level", 1)
            .Set("progress", 0)
            .Set("downgradeTime", (long?)null)
            .Set("safeMode", safeModeExpiry);
        await _roomObjectsCollection.UpdateOneAsync(controllerFilter, controllerUpdate, cancellationToken: cancellationToken).ConfigureAwait(false);

        // Reset sources
        var sourcesFilter = ShardFilterBuilder.ForRoom(bsonFilterBuilder, roomName, shardName) &
                            bsonFilterBuilder.Eq("type", StructureType.Source.ToDocumentValue());
        await _roomObjectsCollection.UpdateManyAsync(
            sourcesFilter,
            Builders<BsonDocument>.Update.Set("invaderHarvested", 0),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        // Insert spawn
        await InsertSpawnAsync(request, roomName, shardName, userId, cancellationToken).ConfigureAwait(false);

        // Update room
        var roomFilter = ShardFilterBuilder.ForRoomId(bsonFilterBuilder, roomName, shardName);
        var roomUpdate = Builders<BsonDocument>.Update
                                               .Set("status", "normal")
                                               .Set("invaderGoal", DefaultInvaderGoal);
        if (!string.IsNullOrWhiteSpace(shardName))
            roomUpdate = roomUpdate.Set("shard", shardName);

        await _roomsCollection.UpdateOneAsync(
            roomFilter,
            roomUpdate,
            new UpdateOptions { IsUpsert = true },
            cancellationToken).ConfigureAwait(false);

        // Update user
        await _usersCollection.UpdateOneAsync(
            Builders<UserDocument>.Filter.Eq(u => u.Id, userId),
            Builders<UserDocument>.Update.Set(u => u.Active, 10000),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        logger.LogInformation("User {UserId} placed spawn in {Room} (shard: {Shard}) at ({X}, {Y})",
            userId, roomName, shardName ?? "default", request.X, request.Y);

        return new PlaceSpawnResult(PlaceSpawnResultStatus.Success);
    }

    private async Task CleanupRoomAsync(string room, string? shard, long gameTime, CancellationToken cancellationToken)
    {
        var filterBuilder = Builders<BsonDocument>.Filter;
        var roomFilter = ShardFilterBuilder.ForRoom(filterBuilder, room, shard);

        var cleanupFilter = roomFilter &
                            filterBuilder.Ne("user", BsonNull.Value) &
                            filterBuilder.In("type", CleanupObjectTypes);

        await _roomObjectsCollection.DeleteManyAsync(cleanupFilter, cancellationToken).ConfigureAwait(false);

        var structuresFilter = roomFilter &
                               filterBuilder.Ne("user", BsonNull.Value) &
                               filterBuilder.Exists("hitsMax");

        var structures = await _roomObjectsCollection.Find(structuresFilter)
                                                     .ToListAsync(cancellationToken)
                                                     .ConfigureAwait(false);
        var ruins = new List<BsonDocument>();
        foreach (var s in structures) {
            if (s["type"].AsString == StructureType.Controller.ToDocumentValue()) continue;

            var ruin = new BsonDocument
            {
                ["_id"] = ObjectId.GenerateNewId(),
                ["type"] = StructureType.Ruin.ToDocumentValue(),
                ["user"] = s["user"],
                ["room"] = s["room"],
                ["x"] = s["x"],
                ["y"] = s["y"],
                ["structure"] = new BsonDocument
                {
                    ["id"] = s["_id"].ToString(),
                    ["type"] = s["type"],
                    ["hits"] = 0,
                    ["hitsMax"] = s["hitsMax"],
                    ["user"] = s["user"]
                },
                ["store"] = s.GetValue("store", new BsonDocument()),
                ["destroyTime"] = gameTime,
                ["decayTime"] = gameTime + 100_000
            };
            if (!string.IsNullOrWhiteSpace(shard))
                ruin["shard"] = shard;

            ruins.Add(ruin);
        }

        if (ruins.Count > 0)
            await _roomObjectsCollection.InsertManyAsync(ruins, cancellationToken: cancellationToken).ConfigureAwait(false);

        await _roomObjectsCollection.DeleteManyAsync(structuresFilter, cancellationToken).ConfigureAwait(false);
    }

    private async Task InsertSpawnAsync(PlaceSpawnRequest request, string room, string? shard, string userId, CancellationToken cancellationToken)
    {
        var spawnDoc = new BsonDocument
        {
            ["_id"] = ObjectId.GenerateNewId(),
            ["type"] = StructureType.Spawn.ToDocumentValue(),
            ["room"] = room,
            ["x"] = request.X,
            ["y"] = request.Y,
            ["name"] = request.Name ?? "Spawn1",
            ["user"] = userId,
            ["store"] = new BsonDocument("energy", SpawnEnergyStart),
            ["storeCapacityResource"] = new BsonDocument("energy", SpawnEnergyCapacity),
            ["hits"] = SpawnHits,
            ["hitsMax"] = SpawnHits,
            ["spawning"] = BsonNull.Value,
            ["notifyWhenAttacked"] = true
        };
        if (!string.IsNullOrWhiteSpace(shard))
            spawnDoc["shard"] = shard;

        await _roomObjectsCollection.InsertOneAsync(spawnDoc, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static FilterDefinition<RoomTerrainDocument> BuildTerrainFilter(string room, string? shard)
    {
        var builder = Builders<RoomTerrainDocument>.Filter;
        var filter = builder.Eq(document => document.Room, room);
        if (!string.IsNullOrWhiteSpace(shard))
            return filter & builder.Eq(document => document.Shard, shard);

        return filter & builder.Or(builder.Eq(document => document.Shard, null),
                                   builder.Exists(nameof(RoomTerrainDocument.Shard), false));
    }

    private static bool IsWall(string? terrain, int x, int y)
    {
        if (string.IsNullOrEmpty(terrain)) return true;
        var index = (y * 50) + x;
        if (index < 0 || index >= terrain.Length) return true;
        return ((terrain[index] - '0') & 1) != 0;
    }
}
