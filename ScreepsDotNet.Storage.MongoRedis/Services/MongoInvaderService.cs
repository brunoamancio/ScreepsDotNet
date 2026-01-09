namespace ScreepsDotNet.Storage.MongoRedis.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

public sealed class MongoInvaderService(IMongoDatabaseProvider databaseProvider, ILogger<MongoInvaderService> logger)
    : IInvaderService
{
    private readonly IMongoCollection<RoomObjectDocument> _roomObjectsCollection = databaseProvider.GetCollection<RoomObjectDocument>(databaseProvider.Settings.RoomObjectsCollection);
    private readonly Random _random = new();

    private static readonly Dictionary<(InvaderSize, InvaderType), BodyPartType[]> BodyTemplates = new()
    {
        [(InvaderSize.Big, InvaderType.Healer)] = [
            BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move,
            BodyPartType.Heal, BodyPartType.Heal, BodyPartType.Heal, BodyPartType.Heal, BodyPartType.Heal, BodyPartType.Heal, BodyPartType.Heal, BodyPartType.Heal, BodyPartType.Heal, BodyPartType.Heal, BodyPartType.Heal, BodyPartType.Heal, BodyPartType.Heal, BodyPartType.Heal, BodyPartType.Heal, BodyPartType.Heal, BodyPartType.Heal, BodyPartType.Heal, BodyPartType.Heal, BodyPartType.Heal, BodyPartType.Heal, BodyPartType.Heal, BodyPartType.Heal, BodyPartType.Heal, BodyPartType.Heal,
            BodyPartType.Move
        ],
        [(InvaderSize.Big, InvaderType.Ranged)] = [
            BodyPartType.Tough, BodyPartType.Tough, BodyPartType.Tough, BodyPartType.Tough, BodyPartType.Tough, BodyPartType.Tough,
            BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move,
            BodyPartType.RangedAttack, BodyPartType.RangedAttack, BodyPartType.RangedAttack, BodyPartType.RangedAttack, BodyPartType.RangedAttack, BodyPartType.RangedAttack, BodyPartType.RangedAttack, BodyPartType.RangedAttack, BodyPartType.RangedAttack, BodyPartType.RangedAttack, BodyPartType.RangedAttack, BodyPartType.RangedAttack, BodyPartType.RangedAttack, BodyPartType.RangedAttack, BodyPartType.RangedAttack, BodyPartType.RangedAttack, BodyPartType.RangedAttack, BodyPartType.RangedAttack,
            BodyPartType.Work, BodyPartType.Move
        ],
        [(InvaderSize.Big, InvaderType.Melee)] = [
            BodyPartType.Tough, BodyPartType.Tough, BodyPartType.Tough, BodyPartType.Tough, BodyPartType.Tough, BodyPartType.Tough, BodyPartType.Tough, BodyPartType.Tough, BodyPartType.Tough, BodyPartType.Tough, BodyPartType.Tough, BodyPartType.Tough, BodyPartType.Tough, BodyPartType.Tough, BodyPartType.Tough, BodyPartType.Tough,
            BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move,
            BodyPartType.RangedAttack, BodyPartType.RangedAttack, BodyPartType.RangedAttack,
            BodyPartType.Work, BodyPartType.Work, BodyPartType.Work, BodyPartType.Work,
            BodyPartType.Attack, BodyPartType.Attack, BodyPartType.Move
        ],
        [(InvaderSize.Small, InvaderType.Healer)] = [
            BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move,
            BodyPartType.Heal, BodyPartType.Heal, BodyPartType.Heal, BodyPartType.Heal, BodyPartType.Heal,
            BodyPartType.Move
        ],
        [(InvaderSize.Small, InvaderType.Ranged)] = [
            BodyPartType.Tough, BodyPartType.Tough,
            BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move,
            BodyPartType.RangedAttack, BodyPartType.RangedAttack, BodyPartType.RangedAttack,
            BodyPartType.Move
        ],
        [(InvaderSize.Small, InvaderType.Melee)] = [
            BodyPartType.Tough, BodyPartType.Tough,
            BodyPartType.Move, BodyPartType.Move, BodyPartType.Move, BodyPartType.Move,
            BodyPartType.RangedAttack, BodyPartType.Work, BodyPartType.Attack, BodyPartType.Move
        ]
    };

    public async Task<CreateInvaderResult> CreateInvaderAsync(string userId, CreateInvaderRequest request, CancellationToken cancellationToken = default)
    {
        if (request.X < 0 || request.X > 49 || request.Y < 0 || request.Y > 49)
            return new CreateInvaderResult(CreateInvaderResultStatus.InvalidParams, ErrorMessage: "Invalid coordinates");

        var roomName = request.Room.Trim();
        var shardName = string.IsNullOrWhiteSpace(request.Shard) ? null : request.Shard.Trim();
        var builder = Builders<RoomObjectDocument>.Filter;
        var roomFilter = BuildRoomFilter(builder, roomName, shardName);

        // Limit check: max 5 invaders per room
        var creepsFilter = roomFilter & builder.Eq(doc => doc.Type, StructureType.Creep.ToDocumentValue());
        var creeps = await _roomObjectsCollection.Find(creepsFilter).ToListAsync(cancellationToken).ConfigureAwait(false);
        var invadersCount = creeps.Count(c => c.UserId == SeedDataDefaults.World.InvaderUser);
        if (invadersCount >= 5)
            return new CreateInvaderResult(CreateInvaderResultStatus.TooManyInvaders);

        // Hostiles check: creeps not belonging to caller or invader
        if (creeps.Any(c => c.UserId != userId && c.UserId != SeedDataDefaults.World.InvaderUser))
            return new CreateInvaderResult(CreateInvaderResultStatus.HostilesPresent);

        // Ownership check
        var controllerFilter = roomFilter & builder.Eq(doc => doc.Type, StructureType.Controller.ToDocumentValue());
        var controller = await _roomObjectsCollection.Find(controllerFilter).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (controller == null)
            return new CreateInvaderResult(CreateInvaderResultStatus.InvalidRoom, ErrorMessage: "Room has no controller");

        var isOwner = controller.UserId == userId;
        var isReserved = controller.Reservation?.UserId == userId;

        if (!isOwner && !isReserved)
            return new CreateInvaderResult(CreateInvaderResultStatus.NotOwned);

        // Position availability
        var positionFilter = roomFilter &
                             builder.Eq(doc => doc.X, request.X) &
                             builder.Eq(doc => doc.Y, request.Y);
        var occupied = await _roomObjectsCollection.Find(positionFilter).AnyAsync(cancellationToken).ConfigureAwait(false);
        if (occupied)
            return new CreateInvaderResult(CreateInvaderResultStatus.InvalidParams, ErrorMessage: "Position occupied");

        // Body generation
        if (!BodyTemplates.TryGetValue((request.Size, request.Type), out var bodyParts))
            return new CreateInvaderResult(CreateInvaderResultStatus.InvalidParams, ErrorMessage: "Invalid invader type or size");

        var body = bodyParts.Select(part => {
            var partDoc = new BsonDocument { ["type"] = part.ToDocumentValue(), ["hits"] = 100 };
            if (request.Boosted) {
                if (part == BodyPartType.Heal) partDoc["boost"] = ResourceBoost.LO.ToDocumentValue();
                else if (part == BodyPartType.RangedAttack) partDoc["boost"] = ResourceBoost.KO.ToDocumentValue();
                else if (part == BodyPartType.Work) partDoc["boost"] = ResourceBoost.ZH.ToDocumentValue();
                else if (part == BodyPartType.Attack) partDoc["boost"] = ResourceBoost.UH.ToDocumentValue();
            }
            return partDoc;
        }).ToArray();

        var invaderId = ObjectId.GenerateNewId();
        var invader = new BsonDocument
        {
            ["_id"] = invaderId,
            ["type"] = StructureType.Creep.ToDocumentValue(),
            ["user"] = SeedDataDefaults.World.InvaderUser,
            ["body"] = new BsonArray(body),
            ["hits"] = bodyParts.Length * 100,
            ["hitsMax"] = bodyParts.Length * 100,
            ["ticksToLive"] = 1500,
            ["x"] = request.X,
            ["y"] = request.Y,
            ["room"] = roomName,
            ["fatigue"] = 0,
            ["store"] = new BsonDocument(),
            ["storeCapacity"] = 0,
            ["name"] = $"invader_{roomName}_{_random.Next(1000)}",
            ["userSummoned"] = userId
        };
        if (!string.IsNullOrWhiteSpace(shardName))
            invader["shard"] = shardName;

        await databaseProvider.GetCollection<BsonDocument>(databaseProvider.Settings.RoomObjectsCollection)
                              .InsertOneAsync(invader, cancellationToken: cancellationToken)
                              .ConfigureAwait(false);

        var displayRoom = string.IsNullOrWhiteSpace(shardName) ? roomName : $"{shardName}/{roomName}";
        logger.LogInformation("User {UserId} created invader {InvaderId} in {Room} at ({X}, {Y})", userId, invaderId, displayRoom, request.X, request.Y);

        return new CreateInvaderResult(CreateInvaderResultStatus.Success, Id: invaderId.ToString());
    }

    public async Task<RemoveInvaderResult> RemoveInvaderAsync(string userId, RemoveInvaderRequest request, CancellationToken cancellationToken = default)
    {
        if (!ObjectId.TryParse(request.Id, out var objectId))
            return new RemoveInvaderResult(RemoveInvaderResultStatus.InvalidObject);

        var filter = Builders<BsonDocument>.Filter.Eq("_id", objectId);
        var invader = await databaseProvider.GetCollection<BsonDocument>(databaseProvider.Settings.RoomObjectsCollection)
                                            .Find(filter)
                                            .FirstOrDefaultAsync(cancellationToken)
                                            .ConfigureAwait(false);

        if (invader == null || invader.GetValue("userSummoned", BsonNull.Value).ToString() != userId)
            return new RemoveInvaderResult(RemoveInvaderResultStatus.InvalidObject);

        await databaseProvider.GetCollection<BsonDocument>(databaseProvider.Settings.RoomObjectsCollection)
                              .DeleteOneAsync(filter, cancellationToken)
                              .ConfigureAwait(false);

        logger.LogInformation("User {UserId} removed invader {InvaderId}", userId, request.Id);

        return new RemoveInvaderResult(RemoveInvaderResultStatus.Success);
    }

    private static FilterDefinition<RoomObjectDocument> BuildRoomFilter(FilterDefinitionBuilder<RoomObjectDocument> builder, string room, string? shard)
    {
        var filter = builder.Eq(doc => doc.Room, room);
        if (!string.IsNullOrWhiteSpace(shard))
            return filter & builder.Eq(doc => doc.Shard, shard);

        var nullShardFilter = builder.Or(
            builder.Eq(doc => doc.Shard, null),
            builder.Eq("shard", BsonNull.Value),
            builder.Exists("shard", false));

        return filter & nullShardFilter;
    }
}
