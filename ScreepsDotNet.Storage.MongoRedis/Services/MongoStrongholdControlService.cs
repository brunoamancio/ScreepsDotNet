namespace ScreepsDotNet.Storage.MongoRedis.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Models.Strongholds;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

public sealed class MongoStrongholdControlService(IMongoDatabaseProvider databaseProvider,
                                                  IStrongholdTemplateProvider templateProvider,
                                                  IWorldMetadataRepository worldMetadataRepository) : IStrongholdControlService
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
    private readonly IMongoCollection<BsonDocument> _roomObjectsCollection = databaseProvider.GetCollection<BsonDocument>(databaseProvider.Settings.RoomObjectsCollection);
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

        var objects = await _roomObjectsCollection.Find(Builders<BsonDocument>.Filter.Eq("room", roomName))
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

        var structures = new List<BsonDocument>();
        foreach (var blueprint in template.Structures) {
            var x = originX + blueprint.OffsetX;
            var y = originY + blueprint.OffsetY;

            switch (blueprint.Type)
            {
                case "invaderCore":
                    structures.Add(BuildInvaderCoreDocument(roomName,
                                                            x,
                                                            y,
                                                            userId,
                                                            template.Name,
                                                            blueprint,
                                                            depositType,
                                                            deployTime,
                                                            strongholdId));
                    break;
                case "rampart":
                    structures.Add(BuildRampartDocument(roomName, x, y, userId, strongholdId, deployTime));
                    break;
            }
        }

        if (structures.Count == 0)
            throw new InvalidOperationException("Selected stronghold template does not contain any structures.");

        await _roomObjectsCollection.InsertManyAsync(structures, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (controller is not null && controller.TryGetValue("_id", out var controllerId)) {
            var filter = Builders<BsonDocument>.Filter.Eq("_id", controllerId);
            var update = Builders<BsonDocument>.Update
                                               .Set("user", userId)
                                               .Set("level", 8)
                                               .Set("progress", 0)
                                               .Set("downgradeTime", deployTime)
                                               .Set("effects", BuildInvulnerabilityEffect(deployTime));

            await _roomObjectsCollection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return new StrongholdSpawnResult(roomName, template.Name, strongholdId);
    }

    public async Task<bool> ExpandAsync(string roomName, CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("room", roomName),
            Builders<BsonDocument>.Filter.Eq("type", "invaderCore"));

        var core = await _roomObjectsCollection.Find(filter)
                                               .FirstOrDefaultAsync(cancellationToken)
                                               .ConfigureAwait(false);
        if (core is null)
            return false;

        var level = core.TryGetValue("level", out var levelValue) && levelValue.IsInt32 ? levelValue.AsInt32 : 1;
        var cooldown = ResolveExpandCooldown(level);
        var gameTime = await worldMetadataRepository.GetGameTimeAsync(cancellationToken).ConfigureAwait(false);
        var update = Builders<BsonDocument>.Update.Set("nextExpandTime", gameTime + cooldown);
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

    private (int x, int y) FindPlacement(IEnumerable<StrongholdStructureBlueprint> structures, string? terrain, IReadOnlyCollection<BsonDocument> currentObjects)
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

    private static BsonDocument BuildInvaderCoreDocument(
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
        var document = new BsonDocument
        {
            ["_id"] = ObjectId.GenerateNewId(),
            ["type"] = "invaderCore",
            ["room"] = roomName,
            ["x"] = x,
            ["y"] = y,
            ["user"] = userId,
            ["level"] = level,
            ["hits"] = InvaderCoreHits,
            ["hitsMax"] = InvaderCoreHits,
            ["templateName"] = templateName,
            ["depositType"] = depositType,
            ["deployTime"] = deployTime,
            ["strongholdId"] = strongholdId,
            ["nextExpandTime"] = deployTime + ResolveExpandCooldown(level),
            ["effects"] = BuildInvulnerabilityEffect(deployTime)
        };

        if (!string.IsNullOrWhiteSpace(blueprint.Behavior))
            document["strongholdBehavior"] = blueprint.Behavior;

        return document;
    }

    private static BsonDocument BuildRampartDocument(string roomName, int x, int y, string userId, string strongholdId, int deployTime)
        => new()
        {
            ["_id"] = ObjectId.GenerateNewId(),
            ["type"] = "rampart",
            ["room"] = roomName,
            ["x"] = x,
            ["y"] = y,
            ["user"] = userId,
            ["hits"] = 1,
            ["hitsMax"] = 1,
            ["isPublic"] = true,
            ["strongholdId"] = strongholdId,
            ["nextDecayTime"] = deployTime
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

    private static BsonDocument? FindController(IEnumerable<BsonDocument> objects)
        => objects.FirstOrDefault(doc => doc.TryGetValue("type", out var typeValue)
                                         && typeValue.IsString
                                         && string.Equals(typeValue.AsString, "controller", StringComparison.Ordinal));

    private static void EnsureRoomIsAvailable(BsonDocument? controller, string intendedUserId)
    {
        if (controller is null)
            return;

        if (controller.TryGetValue("user", out var owner) && !owner.IsBsonNull) {
            if (!string.Equals(owner.ToString(), intendedUserId, StringComparison.Ordinal))
                throw new InvalidOperationException($"Room {controller.GetValue("room", "?")} already has an owner.");
            return;
        }

        if (!controller.TryGetValue("reservation", out var reservationValue) || !reservationValue.IsBsonDocument)
            return;

        var reservation = reservationValue.AsBsonDocument;
        if (!reservation.TryGetValue("user", out var reservationUser) || reservationUser.IsBsonNull)
            return;

        if (!string.Equals(reservationUser.ToString(), intendedUserId, StringComparison.Ordinal))
            throw new InvalidOperationException("Room is reserved by another user.");
    }

    private static bool CanPlaceAll(IEnumerable<StrongholdStructureBlueprint> structures, string terrain, IReadOnlyCollection<BsonDocument> objects, int originX, int originY)
    {
        foreach (var structure in structures) {
            var x = originX + structure.OffsetX;
            var y = originY + structure.OffsetY;
            if (x < 0 || x >= 50 || y < 0 || y >= 50)
                return false;

            if (IsWall(terrain, x, y))
                return false;

            if (objects.Any(obj => obj.TryGetValue("x", out var ox)
                                   && obj.TryGetValue("y", out var oy)
                                   && ox.IsInt32 && oy.IsInt32
                                   && ox.AsInt32 == x
                                   && oy.AsInt32 == y))
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
