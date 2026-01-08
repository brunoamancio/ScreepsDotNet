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
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

public sealed class MongoConstructionService(IMongoDatabaseProvider databaseProvider, ILogger<MongoConstructionService> logger)
    : IConstructionService
{
    private readonly IMongoCollection<BsonDocument> _roomObjectsCollection = databaseProvider.GetCollection<BsonDocument>(databaseProvider.Settings.RoomObjectsCollection);
    private readonly IMongoCollection<RoomTerrainDocument> _roomTerrainCollection = databaseProvider.GetCollection<RoomTerrainDocument>(databaseProvider.Settings.RoomTerrainCollection);

    public async Task<PlaceConstructionResult> CreateConstructionAsync(string userId, PlaceConstructionRequest request, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("CreateConstructionAsync: User={UserId}, Room={Room}, X={X}, Y={Y}, Type={StructureType}", userId, request.Room, request.X, request.Y, request.StructureType);

        if (request.X < 0 || request.X > 49 || request.Y < 0 || request.Y > 49)
            return new PlaceConstructionResult(PlaceConstructionResultStatus.InvalidParams, ErrorMessage: "Invalid coordinates");

        if (!GameConstants.ConstructionCost.ContainsKey(request.StructureType))
            return new PlaceConstructionResult(PlaceConstructionResultStatus.InvalidParams, ErrorMessage: "Invalid structure type");

        if (request.Name is { Length: > 50 })
            return new PlaceConstructionResult(PlaceConstructionResultStatus.InvalidParams, ErrorMessage: "Name too long");

        if (request.StructureType == StructureType.Spawn && string.IsNullOrEmpty(request.Name))
            return new PlaceConstructionResult(PlaceConstructionResultStatus.InvalidParams, ErrorMessage: "Spawn name required");

        // checkConstructionSpot
        var spotResult = await CheckConstructionSpotAsync(request.Room, request.StructureType, request.X, request.Y, cancellationToken).ConfigureAwait(false);
        if (spotResult.Status != PlaceConstructionResultStatus.Success)
            return spotResult;

        // checkController
        var controllerResult = await CheckControllerAsync(request.Room, userId, request.StructureType, cancellationToken).ConfigureAwait(false);
        if (controllerResult.Status != PlaceConstructionResultStatus.Success)
            return controllerResult;

        // check MAX_CONSTRUCTION_SITES
        var userSitesCount = await _roomObjectsCollection.CountDocumentsAsync(
            Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("type", StructureType.ConstructionSite.ToDocumentValue()),
                Builders<BsonDocument>.Filter.Eq("user", userId)
            ), cancellationToken: cancellationToken).ConfigureAwait(false);

        if (userSitesCount >= GameConstants.MaxConstructionSites)
            return new PlaceConstructionResult(PlaceConstructionResultStatus.TooMany);

        // progressTotal calculation
        var progressTotal = GameConstants.ConstructionCost[request.StructureType];
        if (request.StructureType == StructureType.Road)
        {
            var terrainDoc = await _roomTerrainCollection.Find(t => t.Room == request.Room).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (terrainDoc != null)
            {
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
            ["_id"] = siteId,
            ["type"] = StructureType.ConstructionSite.ToDocumentValue(),
            ["room"] = request.Room,
            ["x"] = request.X,
            ["y"] = request.Y,
            ["structureType"] = request.StructureType.ToDocumentValue(),
            ["name"] = (BsonValue?)request.Name ?? BsonNull.Value,
            ["user"] = userId,
            ["progress"] = 0,
            ["progressTotal"] = progressTotal
        };

        await _roomObjectsCollection.InsertOneAsync(siteDoc, cancellationToken: cancellationToken).ConfigureAwait(false);

        logger.LogInformation("User {UserId} created construction site {StructureType} in {Room} at ({X}, {Y})", userId, request.StructureType, request.Room, request.X, request.Y);

        return new PlaceConstructionResult(PlaceConstructionResultStatus.Success, Id: siteId.ToString());
    }

    private async Task<PlaceConstructionResult> CheckConstructionSpotAsync(string room, StructureType structureType, int x, int y, CancellationToken cancellationToken)
    {
        if (x <= 0 || y <= 0 || x >= 49 || y >= 49)
            return new PlaceConstructionResult(PlaceConstructionResultStatus.InvalidLocation);

        var terrainDoc = await _roomTerrainCollection.Find(t => t.Room == room).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (terrainDoc == null)
            return new PlaceConstructionResult(PlaceConstructionResultStatus.InvalidRoom);

        if (structureType == StructureType.Extractor)
        {
            var mineral = await _roomObjectsCollection.Find(Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("room", room),
                Builders<BsonDocument>.Filter.Eq("x", x),
                Builders<BsonDocument>.Filter.Eq("y", y),
                Builders<BsonDocument>.Filter.Eq("type", StructureType.Mineral.ToDocumentValue())
            )).AnyAsync(cancellationToken).ConfigureAwait(false);
            if (!mineral)
                return new PlaceConstructionResult(PlaceConstructionResultStatus.InvalidLocation, ErrorMessage: "Extractor must be on mineral");
        }

        // Check existing same structure or site
        var existing = await _roomObjectsCollection.Find(Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("room", room),
            Builders<BsonDocument>.Filter.Eq("x", x),
            Builders<BsonDocument>.Filter.Eq("y", y),
            Builders<BsonDocument>.Filter.Or(
                Builders<BsonDocument>.Filter.Eq("type", structureType.ToDocumentValue()),
                Builders<BsonDocument>.Filter.Eq("type", StructureType.ConstructionSite.ToDocumentValue())
            )
        )).AnyAsync(cancellationToken).ConfigureAwait(false);
        if (existing)
            return new PlaceConstructionResult(PlaceConstructionResultStatus.InvalidLocation, ErrorMessage: "Position occupied");

        // Check blockers
        if (structureType != StructureType.Rampart)
        {
            var blocker = await _roomObjectsCollection.Find(Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("room", room),
                Builders<BsonDocument>.Filter.Eq("x", x),
                Builders<BsonDocument>.Filter.Eq("y", y),
                Builders<BsonDocument>.Filter.In("type", GameConstants.BlockerStructureTypes.Select(t => t.ToDocumentValue()))
            )).AnyAsync(cancellationToken).ConfigureAwait(false);
            if (blocker)
                return new PlaceConstructionResult(PlaceConstructionResultStatus.InvalidLocation, ErrorMessage: "Position blocked");
        }

        // Check wall terrain
        if (structureType != StructureType.Road)
        {
            if ((GetTerrainAt(terrainDoc.Terrain, x, y) & GameConstants.TerrainMaskWall) != 0)
                return new PlaceConstructionResult(PlaceConstructionResultStatus.InvalidLocation, ErrorMessage: "Cannot build on wall");
        }

        // Check near exit
        var nearExit = await _roomObjectsCollection.Find(Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("room", room),
            Builders<BsonDocument>.Filter.Gt("x", x - 2),
            Builders<BsonDocument>.Filter.Lt("x", x + 2),
            Builders<BsonDocument>.Filter.Gt("y", y - 2),
            Builders<BsonDocument>.Filter.Lt("y", y + 2),
            Builders<BsonDocument>.Filter.Eq("type", StructureType.Exit.ToDocumentValue())
        )).AnyAsync(cancellationToken).ConfigureAwait(false);
        if (nearExit)
            return new PlaceConstructionResult(PlaceConstructionResultStatus.InvalidLocation, ErrorMessage: "Too near exit");

        // Border check for non-road/container
        if (structureType != StructureType.Road && structureType != StructureType.Container && (x == 1 || x == 48 || y == 1 || y == 48))
        {
            List<(int, int)> borderTiles = [];
            if (x == 1) borderTiles.AddRange([(0, y - 1), (0, y), (0, y + 1)]);
            if (x == 48) borderTiles.AddRange([(49, y - 1), (49, y), (49, y + 1)]);
            if (y == 1) borderTiles.AddRange([(x - 1, 0), (x, 0), (x + 1, 0)]);
            if (y == 48) borderTiles.AddRange([(x - 1, 49), (x, 49), (x + 1, 49)]);

            foreach (var (bx, by) in borderTiles)
            {
                if ((GetTerrainAt(terrainDoc.Terrain, bx, by) & GameConstants.TerrainMaskWall) == 0)
                    return new PlaceConstructionResult(PlaceConstructionResultStatus.InvalidLocation, ErrorMessage: "Must be near wall at border");
            }
        }

        return new PlaceConstructionResult(PlaceConstructionResultStatus.Success);
    }

    private async Task<PlaceConstructionResult> CheckControllerAsync(string room, string userId, StructureType structureType, CancellationToken cancellationToken)
    {
        var controller = await _roomObjectsCollection.Find(Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("room", room),
            Builders<BsonDocument>.Filter.Eq("type", StructureType.Controller.ToDocumentValue())
        )).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (controller == null)
            return new PlaceConstructionResult(PlaceConstructionResultStatus.Success); // Room without controller? Legacy allowed it if not spawn

        if (structureType == StructureType.Spawn && controller == null)
            return new PlaceConstructionResult(PlaceConstructionResultStatus.InvalidRoom, ErrorMessage: "Spawn requires controller");

        var owner = controller.Contains("user") && !controller["user"].IsBsonNull ? controller["user"].AsString : null;
        var reservation = controller.GetValue("reservation", BsonNull.Value);
        var reservationUser = reservation.IsBsonDocument && reservation.AsBsonDocument.Contains("user") && !reservation.AsBsonDocument["user"].IsBsonNull
            ? reservation.AsBsonDocument["user"].AsString
            : null;

        if (owner != userId && reservationUser != userId)
            return new PlaceConstructionResult(PlaceConstructionResultStatus.NotControllerOwner);

        // Check RCL limits
        var rcl = controller.GetValue("level", 0).AsInt32;
        if (!GameConstants.ControllerStructures.TryGetValue(structureType, out var limits))
            return new PlaceConstructionResult(PlaceConstructionResultStatus.Success);

        var existingCount = await _roomObjectsCollection.CountDocumentsAsync(
            Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("room", room),
                Builders<BsonDocument>.Filter.Or(
                    Builders<BsonDocument>.Filter.Eq("type", structureType.ToDocumentValue()),
                    Builders<BsonDocument>.Filter.And(
                        Builders<BsonDocument>.Filter.Eq("type", StructureType.ConstructionSite.ToDocumentValue()),
                        Builders<BsonDocument>.Filter.Eq("structureType", structureType.ToDocumentValue())
                    )
                )
            ), cancellationToken: cancellationToken).ConfigureAwait(false);

        if (existingCount >= limits[rcl])
            return new PlaceConstructionResult(PlaceConstructionResultStatus.RclNotEnough);

        return new PlaceConstructionResult(PlaceConstructionResultStatus.Success);
    }

    private static int GetTerrainAt(string? terrain, int x, int y)
    {
        if (string.IsNullOrEmpty(terrain)) return 0;
        var index = (y * 50) + x;
        if (index < 0 || index >= terrain.Length) return 0;
        return terrain[index] - '0';
    }
}
