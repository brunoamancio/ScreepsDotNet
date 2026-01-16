namespace ScreepsDotNet.Driver.Services.Rooms;

using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Extensions;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

internal static class RoomContractMapper
{
    private static readonly IReadOnlyDictionary<string, int> EmptyIntDictionary = new Dictionary<string, int>(0);
    private static readonly IReadOnlyDictionary<string, object?> EmptyObjectDictionary = new Dictionary<string, object?>(0);

    public static IReadOnlyDictionary<string, RoomObjectSnapshot> MapRoomObjects(IReadOnlyDictionary<string, RoomObjectDocument> objects)
    {
        var result = new Dictionary<string, RoomObjectSnapshot>(objects.Count, StringComparer.Ordinal);
        foreach (var (_, document) in objects)
        {
            var state = MapRoomObject(document);
            result[state.Id] = state;
        }

        return result;
    }

    public static RoomObjectSnapshot MapRoomObject(RoomObjectDocument document)
    {
        var store = document.Store is null ? EmptyIntDictionary : new Dictionary<string, int>(document.Store, StringComparer.Ordinal);
        var storeCapacityResource = document.StoreCapacityResource is null
            ? EmptyIntDictionary
            : new Dictionary<string, int>(document.StoreCapacityResource, StringComparer.Ordinal);

        var body = MapBody(document.Body);

        var spawningSnapshot = MapSpawning(document.Spawning);
        bool? isSpawning = document.Spawning is BsonBoolean boolean
            ? boolean.Value
            : null;

        return new RoomObjectSnapshot(
            document.Id.ToString(),
            document.Type ?? string.Empty,
            document.Room ?? string.Empty,
            document.Shard,
            document.UserId,
            document.X ?? 0,
            document.Y ?? 0,
            document.Hits,
            document.HitsMax,
            document.Fatigue,
            document.TicksToLive,
            document.Name,
            document.Level,
            document.Density,
            document.MineralType,
            document.DepositType,
            document.StructureType,
            store,
            document.StoreCapacity,
            storeCapacityResource,
            MapReservation(document.Reservation),
            MapSign(document.Sign),
            MapStructure(document.Structure),
            MapEffects(document.Effects),
            spawningSnapshot,
            body,
            isSpawning,
            document.UserSummoned,
            document.StrongholdId,
            document.DeathTime,
            document.DecayTime,
            document.CreepId,
            document.CreepName,
            document.CreepTicksToLive,
            document.CreepSaying,
            document.ResourceType,
            document.ResourceAmount);
    }

    public static IReadOnlyDictionary<string, UserState> MapUsers(IReadOnlyDictionary<string, UserDocument> users)
    {
        var result = new Dictionary<string, UserState>(users.Count, StringComparer.Ordinal);
        foreach (var (id, document) in users)
        {
            if (string.IsNullOrWhiteSpace(id)) continue;
            result[id] = new UserState(
                id,
                document.Username ?? id,
                document.Cpu ?? 0,
                document.Power ?? 0,
                document.Money ?? 0,
                document.Active.GetValueOrDefault() != 0);
        }

        return result;
    }

    public static RoomInfoSnapshot? MapRoomInfo(RoomDocument? room)
        => room is null
            ? null
            : new RoomInfoSnapshot(
                room.Id,
                room.Shard,
                room.Status,
                room.Novice,
                room.RespawnArea,
                room.OpenTime,
                room.Owner,
                room.Controller?.Level,
                room.EnergyAvailable,
                room.NextNpcMarketOrder,
                room.PowerBankTime,
                room.InvaderGoal);

    private static RoomReservationSnapshot? MapReservation(RoomReservationDocument? document)
        => document is null ? null : new RoomReservationSnapshot(document.UserId, document.EndTime);

    private static RoomSignSnapshot? MapSign(RoomSignDocument? document)
        => document is null ? null : new RoomSignSnapshot(document.UserId, document.Text, document.Time);

    private static RoomObjectStructureSnapshot? MapStructure(RoomObjectStructureDocument? document)
        => document is null
            ? null
            : new RoomObjectStructureSnapshot(document.Id, document.Type, document.UserId, document.Hits, document.HitsMax);

    private static IReadOnlyDictionary<string, object?> MapEffects(BsonArray? effects)
    {
        if (effects is null || effects.Count == 0)
            return EmptyObjectDictionary;

        var result = new Dictionary<string, object?>(effects.Count, StringComparer.Ordinal);
        for (var i = 0; i < effects.Count; i++)
        {
            var effect = effects[i];
            result[i.ToString()] = effect;
        }

        return result;
    }

    public static RoomObjectDocument MapRoomObjectDocument(RoomObjectSnapshot snapshot)
    {
        var document = new RoomObjectDocument
        {
            Id = ObjectId.TryParse(snapshot.Id, out var objectId) ? objectId : ObjectId.GenerateNewId(),
            Type = snapshot.Type,
            Room = snapshot.RoomName,
            Shard = snapshot.Shard,
            UserId = snapshot.UserId,
            X = snapshot.X,
            Y = snapshot.Y,
            Hits = snapshot.Hits,
            HitsMax = snapshot.HitsMax,
            Fatigue = snapshot.Fatigue,
            TicksToLive = snapshot.TicksToLive,
            Name = snapshot.Name,
            Level = snapshot.Level,
            Density = snapshot.Density,
            MineralType = snapshot.MineralType,
            DepositType = snapshot.DepositType,
            StructureType = snapshot.StructureType,
            Store = snapshot.Store.Count == 0 ? null : new Dictionary<string, int>(snapshot.Store, StringComparer.Ordinal),
            StoreCapacity = snapshot.StoreCapacity,
            StoreCapacityResource = snapshot.StoreCapacityResource is null
                ? null
                : new Dictionary<string, int>(snapshot.StoreCapacityResource, StringComparer.Ordinal),
            Reservation = snapshot.Reservation is null
                ? null
                : new RoomReservationDocument
                {
                    UserId = snapshot.Reservation.UserId,
                    EndTime = snapshot.Reservation.EndTime
                },
            Sign = snapshot.Sign is null
                ? null
                : new RoomSignDocument
                {
                    UserId = snapshot.Sign.UserId,
                    Text = snapshot.Sign.Text,
                    Time = snapshot.Sign.Time
                },
            Structure = snapshot.Structure is null
                ? null
                : new RoomObjectStructureDocument
                {
                    Id = snapshot.Structure.Id,
                    Type = snapshot.Structure.Type,
                    Hits = snapshot.Structure.Hits,
                    HitsMax = snapshot.Structure.HitsMax,
                    UserId = snapshot.Structure.UserId
                },
            Body = MapBodyDocuments(snapshot.Body),
            Effects = MapEffectsToBson(snapshot.Effects),
            Spawning = ResolveSpawningValue(snapshot),
            StrongholdId = snapshot.StrongholdId,
            UserSummoned = snapshot.UserSummoned,
            DeathTime = snapshot.DeathTime,
            DecayTime = snapshot.DecayTime,
            CreepId = snapshot.CreepId,
            CreepName = snapshot.CreepName,
            CreepTicksToLive = snapshot.CreepTicksToLive,
            CreepSaying = snapshot.CreepSaying,
            ResourceType = snapshot.ResourceType,
            ResourceAmount = snapshot.ResourceAmount
        };

        return document;
    }

    private static BsonArray? MapEffectsToBson(IReadOnlyDictionary<string, object?>? effects)
    {
        if (effects is null || effects.Count == 0)
            return null;

        var ordered = effects
            .Select(kvp => (Key: kvp.Key, Value: kvp.Value))
            .OrderBy(pair => int.TryParse(pair.Key, out var index) ? index : int.MaxValue)
            .ToArray();

        var array = new BsonArray(ordered.Length);
        foreach (var (_, value) in ordered)
        {
            if (value is BsonValue bsonValue)
                array.Add(bsonValue);
            else
                array.Add(BsonValue.Create(value));
        }

        return array.Count == 0 ? null : array;
    }

    private static RoomSpawnSpawningSnapshot? MapSpawning(BsonValue? spawning)
    {
        if (spawning is null || spawning is BsonNull)
            return null;

        if (spawning is not BsonDocument document)
            return null;

        var name = document.TryGetValue(RoomDocumentFields.RoomObject.SpawningFields.Name, out var nameValue) && nameValue.IsString
            ? nameValue.AsString
            : string.Empty;

        var needTime = TryReadInt(document, RoomDocumentFields.RoomObject.SpawningFields.NeedTime);
        var spawnTime = TryReadInt(document, RoomDocumentFields.RoomObject.SpawningFields.SpawnTime);
        var directions = ReadDirections(document, RoomDocumentFields.RoomObject.SpawningFields.Directions);

        return new RoomSpawnSpawningSnapshot(name, needTime, spawnTime, directions);
    }

    private static BsonValue? ResolveSpawningValue(RoomObjectSnapshot snapshot)
    {
        if (snapshot.Spawning is not null)
            return MapSpawning(snapshot.Spawning);

        return snapshot.IsSpawning.HasValue
            ? BsonBoolean.Create(snapshot.IsSpawning.Value)
            : null;
    }

    private static BsonValue? MapSpawning(RoomSpawnSpawningSnapshot? spawning)
    {
        if (spawning is null)
            return null;

        var document = new BsonDocument
        {
            [RoomDocumentFields.RoomObject.SpawningFields.Name] = spawning.Name ?? string.Empty
        };

        if (spawning.NeedTime.HasValue)
            document[RoomDocumentFields.RoomObject.SpawningFields.NeedTime] = spawning.NeedTime.Value;

        if (spawning.SpawnTime.HasValue)
            document[RoomDocumentFields.RoomObject.SpawningFields.SpawnTime] = spawning.SpawnTime.Value;

        if (spawning.Directions is { Count: > 0 })
        {
            var array = new BsonArray(spawning.Directions.Select(direction => direction.ToInt()));
            document[RoomDocumentFields.RoomObject.SpawningFields.Directions] = array;
        }

        return document;
    }

    private static int? TryReadInt(BsonDocument document, string field)
    {
        if (!document.TryGetValue(field, out var value) || value is null || value.IsBsonNull)
            return null;

        return value switch
        {
            { IsInt32: true } => value.AsInt32,
            { IsInt64: true } => (int)value.AsInt64,
            { IsDouble: true } => (int)value.AsDouble,
            { IsDecimal128: true } => (int)value.AsDecimal128,
            { IsString: true } when int.TryParse(value.AsString, out var parsed) => parsed,
            _ => null
        };
    }

    private static IReadOnlyList<Direction> ReadDirections(BsonDocument document, string field)
    {
        if (!document.TryGetValue(field, out var value) || value is not BsonArray array || array.Count == 0)
            return [];

        var result = new List<Direction>(array.Count);
        var seen = new HashSet<Direction>();
        foreach (var element in array)
        {
            int? raw = element switch
            {
                { IsInt32: true } => element.AsInt32,
                { IsInt64: true } => (int)element.AsInt64,
                { IsDouble: true } => (int)element.AsDouble,
                { IsDecimal128: true } => (int)element.AsDecimal128,
                { IsString: true } when int.TryParse(element.AsString, out var parsed) => parsed,
                _ => null
            };

            if (!raw.HasValue || !DirectionExtensions.TryParseDirection(raw.Value, out var direction))
                continue;

            if (seen.Add(direction))
                result.Add(direction);
        }

        return result.Count == 0 ? Array.Empty<Direction>() : result;
    }

    public static BsonDocument CreateRoomObjectPatchDocument(RoomObjectPatchPayload patch)
    {
        ArgumentNullException.ThrowIfNull(patch);

        var document = new BsonDocument();
        if (patch.Hits.HasValue)
            document[RoomDocumentFields.RoomObject.Hits] = patch.Hits.Value;

        if (patch.Position is { } position)
        {
            if (position.X.HasValue)
                document[RoomDocumentFields.RoomObject.X] = position.X.Value;
            if (position.Y.HasValue)
                document[RoomDocumentFields.RoomObject.Y] = position.Y.Value;
        }

        if (patch.Fatigue.HasValue)
            document[RoomDocumentFields.RoomObject.Fatigue] = patch.Fatigue.Value;

        if (patch.DowngradeTimer.HasValue)
            document[RoomDocumentFields.RoomObject.DowngradeTimer] = patch.DowngradeTimer.Value;

        if (patch.UpgradeBlocked.HasValue)
            document[RoomDocumentFields.RoomObject.UpgradeBlocked] = patch.UpgradeBlocked.Value;

        if (patch.SpawnCooldownTime.HasValue)
            document[RoomDocumentFields.RoomObject.SpawnCooldownTime] = patch.SpawnCooldownTime.Value;

        if (patch.StructureHits.HasValue)
            document[RoomDocumentFields.RoomObject.StructureHits] = patch.StructureHits.Value;

        if (patch.TicksToLive.HasValue)
            document[RoomDocumentFields.RoomObject.TicksToLive] = patch.TicksToLive.Value;

        if (patch.ActionLog is { } actionLog && actionLog.HasEntries)
        {
            var logDocument = new BsonDocument();
            if (actionLog.Die is { } die)
            {
                logDocument[RoomDocumentFields.RoomObject.ActionLogFields.Die] = new BsonDocument
                {
                    [RoomDocumentFields.RoomObject.ActionLogFields.Time] = die.Time
                };
            }

            if (logDocument.ElementCount > 0)
                document[RoomDocumentFields.RoomObject.ActionLog] = logDocument;
        }

        if (patch.Store is { Count: > 0 })
        {
            foreach (var (resource, amount) in patch.Store)
            {
                if (string.IsNullOrWhiteSpace(resource))
                    continue;

                var field = $"{RoomDocumentFields.RoomObject.Store.Root}.{resource}";
                document[field] = amount;
            }
        }

        if (patch.StoreCapacity.HasValue)
            document[RoomDocumentFields.RoomObject.Store.Capacity] = patch.StoreCapacity.Value;

        if (patch.Body is { Count: > 0 })
        {
            var bodyArray = CreateBodyArray(patch.Body);
            if (bodyArray is not null)
                document[RoomDocumentFields.RoomObject.Body] = bodyArray;
        }

        if (patch.Spawning is not null)
        {
            if (MapSpawning(patch.Spawning) is BsonDocument spawningDocument)
                document[RoomDocumentFields.RoomObject.Spawning] = spawningDocument;
        }
        else if (patch.ClearSpawning)
            document[RoomDocumentFields.RoomObject.Spawning] = BsonNull.Value;

        return document;
    }

    public static BsonDocument CreateRoomInfoPatchDocument(RoomInfoPatchPayload patch)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var document = new BsonDocument();

        if (patch.Status is not null)
            document[RoomDocumentFields.Info.Status] = patch.Status;

        if (patch.IsNoviceArea.HasValue)
            document[RoomDocumentFields.Info.Novice] = patch.IsNoviceArea.Value;

        if (patch.IsRespawnArea.HasValue)
            document[RoomDocumentFields.Info.RespawnArea] = patch.IsRespawnArea.Value;

        if (patch.OpenTime.HasValue)
            document[RoomDocumentFields.Info.OpenTime] = patch.OpenTime.Value;

        if (patch.OwnerUserId is not null)
            document[RoomDocumentFields.Info.Owner] = patch.OwnerUserId;

        if (patch.ControllerLevel.HasValue)
        {
            document[RoomDocumentFields.Info.Controller] = new BsonDocument
            {
                [RoomDocumentFields.Info.ControllerLevel] = patch.ControllerLevel.Value
            };
        }

        if (patch.EnergyAvailable.HasValue)
            document[RoomDocumentFields.Info.EnergyAvailable] = patch.EnergyAvailable.Value;

        if (patch.NextNpcMarketOrder.HasValue)
            document[RoomDocumentFields.Info.NextNpcMarketOrder] = patch.NextNpcMarketOrder.Value;

        if (patch.PowerBankTime.HasValue)
            document[RoomDocumentFields.Info.PowerBankTime] = patch.PowerBankTime.Value;

        if (patch.InvaderGoal.HasValue)
            document[RoomDocumentFields.Info.InvaderGoal] = patch.InvaderGoal.Value;

        return document;
    }

    private static IReadOnlyList<CreepBodyPartSnapshot> MapBody(IReadOnlyList<RoomObjectBodyPartDocument>? bodyParts)
    {
        if (bodyParts is null || bodyParts.Count == 0)
            return [];

        var result = new List<CreepBodyPartSnapshot>(bodyParts.Count);
        foreach (var part in bodyParts)
        {
            if (string.IsNullOrWhiteSpace(part.Type))
                continue;

            if (!part.Type.TryParseBodyPartType(out var type))
                continue;

            var hits = part.Hits ?? ScreepsGameConstants.BodyPartHitPoints;
            result.Add(new CreepBodyPartSnapshot(type, hits, string.IsNullOrWhiteSpace(part.Boost) ? null : part.Boost));
        }

        return result.Count == 0 ? Array.Empty<CreepBodyPartSnapshot>() : result;
    }

    private static List<RoomObjectBodyPartDocument>? MapBodyDocuments(IReadOnlyList<CreepBodyPartSnapshot> body)
    {
        if (body.Count == 0)
            return null;

        var result = new List<RoomObjectBodyPartDocument>(body.Count);
        foreach (var part in body)
        {
            result.Add(new RoomObjectBodyPartDocument
            {
                Type = part.Type.ToDocumentValue(),
                Hits = part.Hits,
                Boost = part.Boost
            });
        }

        return result;
    }

    private static BsonArray? CreateBodyArray(IReadOnlyList<CreepBodyPartSnapshot> body)
    {
        if (body.Count == 0)
            return null;

        var array = new BsonArray(body.Count);
        foreach (var part in body)
        {
            var document = new BsonDocument
            {
                [RoomDocumentFields.RoomObject.BodyPart.Type] = part.Type.ToDocumentValue(),
                [RoomDocumentFields.RoomObject.BodyPart.Hits] = part.Hits
            };

            if (!string.IsNullOrWhiteSpace(part.Boost))
                document[RoomDocumentFields.RoomObject.BodyPart.Boost] = part.Boost;

            array.Add(document);
        }

        return array;
    }
}
