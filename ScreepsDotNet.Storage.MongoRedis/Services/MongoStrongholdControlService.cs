namespace ScreepsDotNet.Storage.MongoRedis.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Backend.Core.Models.Strongholds;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

public sealed class MongoStrongholdControlService(IMongoDatabaseProvider databaseProvider, IStrongholdTemplateProvider templateProvider, IWorldMetadataRepository worldMetadataRepository)
    : IStrongholdControlService
{
    private const string DefaultInvaderUserId = "2";
    private const int MaxAttempts = 200;
    private const int MinCoord = 4;
    private const int MaxCoord = 45;
    private const int InvaderCoreHits = 100_000;
    private const int DefaultDeployDelay = 1;
    private const int StrongholdDeployDuration = 5_000;
    private const int InvulnerabilityEffectId = 1_001;
    private static readonly IReadOnlyDictionary<int, int> ExpandCooldownByLevel = new Dictionary<int, int>
    {
        [1] = 4_000,
        [2] = 3_500,
        [3] = 3_000,
        [4] = 2_500,
        [5] = 2_000
    };

    private readonly IMongoCollection<RoomTerrainDocument> _terrainCollection = databaseProvider.GetCollection<RoomTerrainDocument>(databaseProvider.Settings.RoomTerrainCollection);
    private readonly IMongoCollection<RoomObjectDocument> _roomObjectsCollection = databaseProvider.GetCollection<RoomObjectDocument>(databaseProvider.Settings.RoomObjectsCollection);
    private readonly Random _random = new();

    public async Task<StrongholdSpawnResult> SpawnAsync(string roomName, StrongholdSpawnOptions options, CancellationToken cancellationToken = default)
    {
        var template = await ResolveTemplateAsync(options.TemplateName, cancellationToken).ConfigureAwait(false);
        var depositTypes = await templateProvider.GetDepositTypesAsync(cancellationToken).ConfigureAwait(false);
        if (depositTypes.Count == 0)
            throw new InvalidOperationException("Stronghold deposit metadata is missing.");

        var terrain = await _terrainCollection.Find(document => document.Room == roomName)
                                              .FirstOrDefaultAsync(cancellationToken)
                                              .ConfigureAwait(false)
                      ?? throw new InvalidOperationException($"Terrain data for room {roomName} is missing.");

        var objects = await _roomObjectsCollection.Find(Builders<RoomObjectDocument>.Filter.Eq(doc => doc.Room, roomName))
                                                  .ToListAsync(cancellationToken)
                                                  .ConfigureAwait(false);
        var controller = FindController(objects);

        var userId = string.IsNullOrWhiteSpace(options.OwnerUserId) ? DefaultInvaderUserId : options.OwnerUserId;
        EnsureRoomIsAvailable(controller, userId);

        var (originX, originY) = options.X.HasValue && options.Y.HasValue
            ? (options.X.Value, options.Y.Value)
            : FindPlacement(template.Structures, terrain.Terrain, objects);

        var deployDelay = options.DeployDelayTicks.GetValueOrDefault(DefaultDeployDelay);
        var gameTime = await worldMetadataRepository.GetGameTimeAsync(cancellationToken).ConfigureAwait(false);
        var deployTime = gameTime + deployDelay;
        var strongholdId = $"{roomName}_{gameTime}";
        var depositType = depositTypes[_random.Next(depositTypes.Count)];

        var structures = new List<RoomObjectDocument>();
        foreach (var blueprint in template.Structures) {
            var x = originX + blueprint.OffsetX;
            var y = originY + blueprint.OffsetY;

            if (blueprint.Type == StructureType.InvaderCore) {
                structures.Add(BuildInvaderCoreDocument(roomName,
                                                        x,
                                                        y,
                                                        userId,
                                                        template.Name,
                                                        blueprint,
                                                        depositType,
                                                        deployTime,
                                                        strongholdId));
            }
            else if (blueprint.Type == StructureType.Rampart) structures.Add(BuildRampartDocument(roomName, x, y, userId, strongholdId, deployTime));
        }

        if (structures.Count == 0)
            throw new InvalidOperationException("Selected stronghold template does not contain any structures.");

        await _roomObjectsCollection.InsertManyAsync(structures, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (controller is not null) {
            var filter = Builders<RoomObjectDocument>.Filter.Eq(doc => doc.Id, controller.Id);
            var update = Builders<RoomObjectDocument>.Update
                                               .Set(doc => doc.UserId, userId)
                                               .Set(doc => doc.Level, 8)
                                               .Set(doc => doc.Progress, 0)
                                               .Set(doc => doc.DowngradeTime, deployTime)
                                               .Set(doc => doc.Effects, BuildInvulnerabilityEffect(deployTime));

            await _roomObjectsCollection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return new StrongholdSpawnResult(roomName, template.Name, strongholdId);
    }

    public async Task<bool> ExpandAsync(string roomName, CancellationToken cancellationToken = default)
    {
        var filter = Builders<RoomObjectDocument>.Filter.And(
            Builders<RoomObjectDocument>.Filter.Eq(doc => doc.Room, roomName),
            Builders<RoomObjectDocument>.Filter.Eq(doc => doc.Type, StructureType.InvaderCore.ToDocumentValue()));

        var core = await _roomObjectsCollection.Find(filter)
                                               .FirstOrDefaultAsync(cancellationToken)
                                               .ConfigureAwait(false);
        if (core is null)
            return false;

        var level = core.Level ?? 1;
        var cooldown = ResolveExpandCooldown(level);
        var gameTime = await worldMetadataRepository.GetGameTimeAsync(cancellationToken).ConfigureAwait(false);
        var update = Builders<RoomObjectDocument>.Update.Set(doc => doc.NextExpandTime, gameTime + cooldown);
        await _roomObjectsCollection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task<StrongholdTemplate> ResolveTemplateAsync(string? requestedName, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(requestedName)) {
            var template = await templateProvider.FindTemplateAsync(requestedName, cancellationToken).ConfigureAwait(false);
            return template ?? throw new InvalidOperationException($"Stronghold template '{requestedName}' not found.");
        }

        var templates = await templateProvider.GetTemplatesAsync(cancellationToken).ConfigureAwait(false);
        if (templates.Count == 0)
            throw new InvalidOperationException("No stronghold templates are available.");

        return templates[_random.Next(templates.Count)];
    }

    private (int x, int y) FindPlacement(IEnumerable<StrongholdStructureBlueprint> structures, string? terrain, IReadOnlyCollection<RoomObjectDocument> currentObjects)
    {
        if (terrain is null)
            throw new InvalidOperationException("Terrain data unavailable.");

        for (var attempt = 0; attempt < MaxAttempts; attempt++) {
            var originX = _random.Next(MinCoord, MaxCoord);
            var originY = _random.Next(MinCoord, MaxCoord);

            if (CanPlaceAll(structures, terrain, currentObjects, originX, originY))
                return (originX, originY);
        }

        throw new InvalidOperationException("Unable to locate a valid placement for the stronghold.");
    }

    private static int ResolveExpandCooldown(int level)
        => ExpandCooldownByLevel.TryGetValue(level, out var cooldown)
            ? cooldown
            : ExpandCooldownByLevel[1];

    private static RoomObjectDocument BuildInvaderCoreDocument(
        string roomName,
        int x,
        int y,
        string userId,
        string templateName,
        StrongholdStructureBlueprint blueprint,
        string depositType,
        int deployTime,
        string strongholdId)
    {
        var level = blueprint.Level ?? 1;
        var document = new RoomObjectDocument
        {
            Id = ObjectId.GenerateNewId(),
            Type = StructureType.InvaderCore.ToDocumentValue(),
            Room = roomName,
            X = x,
            Y = y,
            UserId = userId,
            Level = level,
            Hits = InvaderCoreHits,
            HitsMax = InvaderCoreHits,
            TemplateName = templateName,
            DepositType = depositType,
            DeployTime = deployTime,
            StrongholdId = strongholdId,
            NextExpandTime = deployTime + ResolveExpandCooldown(level),
            Effects = BuildInvulnerabilityEffect(deployTime),
            StrongholdBehavior = blueprint.Behavior
        };

        return document;
    }

    private static RoomObjectDocument BuildRampartDocument(string roomName, int x, int y, string userId, string strongholdId, int deployTime)
        => new()
        {
            Id = ObjectId.GenerateNewId(),
            Type = StructureType.Rampart.ToDocumentValue(),
            Room = roomName,
            X = x,
            Y = y,
            UserId = userId,
            Hits = 1,
            HitsMax = 1,
            StrongholdId = strongholdId,
            NextExpandTime = deployTime // nextDecayTime in legacy
        };

    private static BsonArray BuildInvulnerabilityEffect(int deployTime)
        => new([
            new BsonDocument
            {
                ["effect"] = InvulnerabilityEffectId,
                ["power"] = InvulnerabilityEffectId,
                ["endTime"] = deployTime,
                ["duration"] = StrongholdDeployDuration
            }
        ]);

    private static RoomObjectDocument? FindController(IEnumerable<RoomObjectDocument> objects)
        => objects.FirstOrDefault(doc => string.Equals(doc.Type, StructureType.Controller.ToDocumentValue(), StringComparison.Ordinal));

    private static void EnsureRoomIsAvailable(RoomObjectDocument? controller, string intendedUserId)
    {
        if (controller is null)
            return;

        if (controller.UserId is not null) {
            if (!string.Equals(controller.UserId, intendedUserId, StringComparison.Ordinal))
                throw new InvalidOperationException($"Room {controller.Room ?? "?"} already has an owner.");
            return;
        }

        if (controller.Reservation is null)
            return;

        if (controller.Reservation.UserId is null)
            return;

        if (!string.Equals(controller.Reservation.UserId, intendedUserId, StringComparison.Ordinal))
            throw new InvalidOperationException("Room is reserved by another user.");
    }

    private static bool CanPlaceAll(IEnumerable<StrongholdStructureBlueprint> structures, string terrain, IReadOnlyCollection<RoomObjectDocument> objects, int originX, int originY)
    {
        foreach (var structure in structures) {
            var x = originX + structure.OffsetX;
            var y = originY + structure.OffsetY;
            if (x < 0 || x >= 50 || y < 0 || y >= 50)
                return false;

            if (IsWall(terrain, x, y))
                return false;

            if (objects.Any(obj => obj.X == x && obj.Y == y))
                return false;
        }

        return true;
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
}
