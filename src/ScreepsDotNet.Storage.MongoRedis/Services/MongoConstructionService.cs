namespace ScreepsDotNet.Storage.MongoRedis.Services;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

public sealed class MongoConstructionService(IMongoDatabaseProvider databaseProvider, ILogger<MongoConstructionService> logger)
    : IConstructionService
{
    private readonly IMongoCollection<BsonDocument> _roomObjectsCollection = databaseProvider.GetCollection<BsonDocument>(databaseProvider.Settings.RoomObjectsCollection);
    private readonly IMongoCollection<RoomTerrainDocument> _roomTerrainCollection = databaseProvider.GetCollection<RoomTerrainDocument>(databaseProvider.Settings.RoomTerrainCollection);

    public async Task<PlaceConstructionResult> CreateConstructionAsync(string userId, PlaceConstructionRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Room))
            return new PlaceConstructionResult(PlaceConstructionResultStatus.InvalidParams, ErrorMessage: "Invalid room");

        var roomName = request.Room.Trim();
        var shardName = string.IsNullOrWhiteSpace(request.Shard) ? null : request.Shard.Trim();

        logger.LogDebug("CreateConstructionAsync: User={UserId}, Room={Room}, Shard={Shard}, X={X}, Y={Y}, Type={StructureType}",
            userId, roomName, shardName ?? "default", request.X, request.Y, request.StructureType);

        if (request.X < 0 || request.X > 49 || request.Y < 0 || request.Y > 49)
            return new PlaceConstructionResult(PlaceConstructionResultStatus.InvalidParams, ErrorMessage: "Invalid coordinates");

        if (!GameConstants.ConstructionCost.ContainsKey(request.StructureType))
            return new PlaceConstructionResult(PlaceConstructionResultStatus.InvalidParams, ErrorMessage: "Invalid structure type");

        if (request.Name is { Length: > 50 })
            return new PlaceConstructionResult(PlaceConstructionResultStatus.InvalidParams, ErrorMessage: "Name too long");

        if (request.StructureType == StructureType.Spawn && string.IsNullOrEmpty(request.Name))
            return new PlaceConstructionResult(PlaceConstructionResultStatus.InvalidParams, ErrorMessage: "Spawn name required");

        // checkConstructionSpot
        var spotResult = await CheckConstructionSpotAsync(roomName, shardName, request.StructureType, request.X, request.Y, cancellationToken).ConfigureAwait(false);
        if (spotResult.Status != PlaceConstructionResultStatus.Success)
            return spotResult;

        // checkController
        var controllerResult = await CheckControllerAsync(roomName, shardName, userId, request.StructureType, cancellationToken).ConfigureAwait(false);
        if (controllerResult.Status != PlaceConstructionResultStatus.Success)
            return controllerResult;

        // check MAX_CONSTRUCTION_SITES
        var userSitesCount = await _roomObjectsCollection.CountDocumentsAsync(
            Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq(RoomDocumentFields.RoomObject.Type, StructureType.ConstructionSite.ToDocumentValue()),
                Builders<BsonDocument>.Filter.Eq(RoomDocumentFields.RoomObject.User, userId)
            ), cancellationToken: cancellationToken).ConfigureAwait(false);

        if (userSitesCount >= GameConstants.MaxConstructionSites)
            return new PlaceConstructionResult(PlaceConstructionResultStatus.TooMany);

        // progressTotal calculation
        var progressTotal = GameConstants.ConstructionCost[request.StructureType];
        if (request.StructureType == StructureType.Road) {
            var terrainFilter = BuildTerrainFilter(roomName, shardName);
            var terrainDoc = await _roomTerrainCollection.Find(terrainFilter).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (terrainDoc != null) {
                var terrainValue = GetTerrainAt(terrainDoc.Terrain, request.X, request.Y);
                if ((terrainValue & GameConstants.TerrainMaskSwamp) != 0)
                    progressTotal *= GameConstants.ConstructionCostRoadSwampRatio;
                else if ((terrainValue & GameConstants.TerrainMaskWall) != 0)
                    progressTotal *= GameConstants.ConstructionCostRoadWallRatio;
            }
        }

        // Insert
        var siteId = ObjectId.GenerateNewId();
        var siteDoc = new BsonDocument
        {
            [RoomDocumentFields.RoomObject.Id] = siteId,
            [RoomDocumentFields.RoomObject.Type] = StructureType.ConstructionSite.ToDocumentValue(),
            [RoomDocumentFields.RoomObject.Room] = roomName,
            [RoomDocumentFields.RoomObject.X] = request.X,
            [RoomDocumentFields.RoomObject.Y] = request.Y,
            [RoomDocumentFields.RoomObject.StructureType] = request.StructureType.ToDocumentValue(),
            [RoomDocumentFields.RoomObject.Name] = (BsonValue?)request.Name ?? BsonNull.Value,
            [RoomDocumentFields.RoomObject.User] = userId,
            [RoomDocumentFields.RoomObject.Progress] = 0,
            [RoomDocumentFields.RoomObject.ProgressTotal] = progressTotal
        };
        if (!string.IsNullOrWhiteSpace(shardName))
            siteDoc[RoomDocumentFields.RoomObject.Shard] = shardName;

        await _roomObjectsCollection.InsertOneAsync(siteDoc, cancellationToken: cancellationToken).ConfigureAwait(false);

        logger.LogInformation("User {UserId} created construction site {StructureType} in {Room} (shard: {Shard}) at ({X}, {Y})",
            userId, request.StructureType, roomName, shardName ?? "default", request.X, request.Y);

        return new PlaceConstructionResult(PlaceConstructionResultStatus.Success, Id: siteId.ToString());
    }

    private async Task<PlaceConstructionResult> CheckConstructionSpotAsync(string room, string? shard, StructureType structureType, int x, int y, CancellationToken cancellationToken)
    {
        if (x <= 0 || y <= 0 || x >= 49 || y >= 49)
            return new PlaceConstructionResult(PlaceConstructionResultStatus.InvalidLocation);

        var terrainFilter = BuildTerrainFilter(room, shard);
        var terrainDoc = await _roomTerrainCollection.Find(terrainFilter).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (terrainDoc == null)
            return new PlaceConstructionResult(PlaceConstructionResultStatus.InvalidRoom);

        var builder = Builders<BsonDocument>.Filter;
        var baseRoomFilter = ShardFilterBuilder.ForRoom(builder, room, shard);

        if (structureType == StructureType.Extractor) {
            var mineralFilter = baseRoomFilter &
                                builder.Eq(RoomDocumentFields.RoomObject.X, x) &
                                builder.Eq(RoomDocumentFields.RoomObject.Y, y) &
                                builder.Eq(RoomDocumentFields.RoomObject.Type, StructureType.Mineral.ToDocumentValue());
            var mineral = await _roomObjectsCollection.Find(mineralFilter).AnyAsync(cancellationToken).ConfigureAwait(false);
            if (!mineral)
                return new PlaceConstructionResult(PlaceConstructionResultStatus.InvalidLocation, ErrorMessage: "Extractor must be on mineral");
        }

        // Check existing same structure or site
        var existingFilter = baseRoomFilter &
                             builder.Eq(RoomDocumentFields.RoomObject.X, x) &
                             builder.Eq(RoomDocumentFields.RoomObject.Y, y) &
                             builder.Or(
                                 builder.Eq(RoomDocumentFields.RoomObject.Type, structureType.ToDocumentValue()),
                                 builder.And(
                                     builder.Eq(RoomDocumentFields.RoomObject.Type, StructureType.ConstructionSite.ToDocumentValue()),
                                     builder.Eq(RoomDocumentFields.RoomObject.StructureType, structureType.ToDocumentValue())
                                 )
                             );
        var existing = await _roomObjectsCollection.Find(existingFilter).AnyAsync(cancellationToken).ConfigureAwait(false);
        if (existing)
            return new PlaceConstructionResult(PlaceConstructionResultStatus.InvalidLocation, ErrorMessage: "Position occupied");

        // Check blockers
        if (structureType != StructureType.Rampart) {
            var blockerFilter = baseRoomFilter &
                                builder.Eq(RoomDocumentFields.RoomObject.X, x) &
                                builder.Eq(RoomDocumentFields.RoomObject.Y, y) &
                                builder.In(RoomDocumentFields.RoomObject.Type, GameConstants.BlockerStructureTypes.Select(t => t.ToDocumentValue()));
            var blocker = await _roomObjectsCollection.Find(blockerFilter).AnyAsync(cancellationToken).ConfigureAwait(false);
            if (blocker)
                return new PlaceConstructionResult(PlaceConstructionResultStatus.InvalidLocation, ErrorMessage: "Position blocked");
        }

        // Check wall terrain
        if (structureType != StructureType.Road) {
            if ((GetTerrainAt(terrainDoc.Terrain, x, y) & GameConstants.TerrainMaskWall) != 0)
                return new PlaceConstructionResult(PlaceConstructionResultStatus.InvalidLocation, ErrorMessage: "Cannot build on wall");
        }

        // Check near exit
        var nearExitFilter = baseRoomFilter &
                             builder.Gt(RoomDocumentFields.RoomObject.X, x - 2) &
                             builder.Lt(RoomDocumentFields.RoomObject.X, x + 2) &
                             builder.Gt(RoomDocumentFields.RoomObject.Y, y - 2) &
                             builder.Lt(RoomDocumentFields.RoomObject.Y, y + 2) &
                             builder.Eq(RoomDocumentFields.RoomObject.Type, StructureType.Exit.ToDocumentValue());
        var nearExit = await _roomObjectsCollection.Find(nearExitFilter).AnyAsync(cancellationToken).ConfigureAwait(false);
        if (nearExit)
            return new PlaceConstructionResult(PlaceConstructionResultStatus.InvalidLocation, ErrorMessage: "Too near exit");

        // Border check for non-road/container
        if (structureType != StructureType.Road && structureType != StructureType.Container && (x == 1 || x == 48 || y == 1 || y == 48)) {
            List<(int, int)> borderTiles = [];
            if (x == 1) borderTiles.AddRange([(0, y - 1), (0, y), (0, y + 1)]);
            if (x == 48) borderTiles.AddRange([(49, y - 1), (49, y), (49, y + 1)]);
            if (y == 1) borderTiles.AddRange([(x - 1, 0), (x, 0), (x + 1, 0)]);
            if (y == 48) borderTiles.AddRange([(x - 1, 49), (x, 49), (x + 1, 49)]);

            foreach (var (bx, by) in borderTiles) {
                if ((GetTerrainAt(terrainDoc.Terrain, bx, by) & GameConstants.TerrainMaskWall) == 0)
                    return new PlaceConstructionResult(PlaceConstructionResultStatus.InvalidLocation, ErrorMessage: "Must be near wall at border");
            }
        }

        return new PlaceConstructionResult(PlaceConstructionResultStatus.Success);
    }

    private async Task<PlaceConstructionResult> CheckControllerAsync(string room, string? shard, string userId, StructureType structureType, CancellationToken cancellationToken)
    {
        var builder = Builders<BsonDocument>.Filter;
        var controllerFilter = ShardFilterBuilder.ForRoom(builder, room, shard) &
                               builder.Eq(RoomDocumentFields.RoomObject.Type, StructureType.Controller.ToDocumentValue());
        var controller = await _roomObjectsCollection.Find(controllerFilter)
                                                     .FirstOrDefaultAsync(cancellationToken)
                                                     .ConfigureAwait(false);

        if (controller == null)
            return new PlaceConstructionResult(PlaceConstructionResultStatus.Success); // Room without controller? Legacy allowed it if not spawn

        if (structureType == StructureType.Spawn && controller == null)
            return new PlaceConstructionResult(PlaceConstructionResultStatus.InvalidRoom, ErrorMessage: "Spawn requires controller");

        var owner = controller.Contains(RoomDocumentFields.RoomObject.User) && !controller[RoomDocumentFields.RoomObject.User].IsBsonNull ? controller[RoomDocumentFields.RoomObject.User].AsString : null;
        var reservation = controller.GetValue(RoomDocumentFields.RoomObject.Reservation, BsonNull.Value);
        var reservationUser = reservation.IsBsonDocument && reservation.AsBsonDocument.Contains(RoomDocumentFields.RoomObject.ReservationFields.User) && !reservation.AsBsonDocument[RoomDocumentFields.RoomObject.ReservationFields.User].IsBsonNull
            ? reservation.AsBsonDocument[RoomDocumentFields.RoomObject.ReservationFields.User].AsString
            : null;

        if (owner != userId && reservationUser != userId)
            return new PlaceConstructionResult(PlaceConstructionResultStatus.NotControllerOwner);

        // Check RCL limits
        var rcl = controller.GetValue(RoomDocumentFields.Controller.Level, 0).AsInt32;
        if (!GameConstants.ControllerStructures.TryGetValue(structureType, out var limits))
            return new PlaceConstructionResult(PlaceConstructionResultStatus.Success);

        var countFilter = ShardFilterBuilder.ForRoom(builder, room, shard) &
                          builder.Or(
                              builder.Eq(RoomDocumentFields.RoomObject.Type, structureType.ToDocumentValue()),
                              builder.And(
                                  builder.Eq(RoomDocumentFields.RoomObject.Type, StructureType.ConstructionSite.ToDocumentValue()),
                                  builder.Eq(RoomDocumentFields.RoomObject.StructureType, structureType.ToDocumentValue())
                              )
                          );

        var existingCount = await _roomObjectsCollection.CountDocumentsAsync(countFilter, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (existingCount >= limits[rcl])
            return new PlaceConstructionResult(PlaceConstructionResultStatus.RclNotEnough);

        return new PlaceConstructionResult(PlaceConstructionResultStatus.Success);
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

    private static int GetTerrainAt(string? terrain, int x, int y)
    {
        if (string.IsNullOrEmpty(terrain)) return 0;
        var index = (y * 50) + x;
        if (index < 0 || index >= terrain.Length) return 0;
        return terrain[index] - '0';
    }
}
