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

    public static IReadOnlyDictionary<string, RoomObjectSnapshot> MapRoomObjects(IReadOnlyDictionary<string, RoomObjectDocument> objects)
    {
        var result = new Dictionary<string, RoomObjectSnapshot>(objects.Count, StringComparer.Ordinal);
        foreach (var (_, document) in objects) {
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
        var actionLog = MapActionLog(document.ActionLog);

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
            document.IsPublic,
            document.StrongholdId,
            document.DeathTime,
            document.DecayTime,
            document.CreepId,
            document.CreepName,
            document.CreepTicksToLive,
            document.CreepSaying,
            document.ResourceType,
            document.ResourceAmount,
            document.Progress,
            document.ProgressTotal,
            actionLog,
            document.Energy,
            document.MineralAmount,
            document.InvaderHarvested,
            document.Harvested,
            document.Cooldown,
            document.CooldownTime,
            document.SafeMode,
            MapPortalDestination(document.Destination),
            MapSend(document.Send));
    }

    public static IReadOnlyDictionary<string, UserState> MapUsers(IReadOnlyDictionary<string, UserDocument> users)
    {
        var result = new Dictionary<string, UserState>(users.Count, StringComparer.Ordinal);
        foreach (var (id, document) in users) {
            if (string.IsNullOrWhiteSpace(id)) continue;
            result[id] = new UserState(
                id,
                document.Username ?? id,
                document.Cpu ?? 0,
                document.Power ?? 0,
                document.Money ?? 0,
                document.Active.GetValueOrDefault() != 0,
                document.PowerExperimentationTime ?? 0,
                document.Resources ?? new Dictionary<string, int>(StringComparer.Ordinal));
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

    private static RoomPortalDestinationSnapshot? MapPortalDestination(BsonDocument? document)
    {
        if (document is null)
            return null;

        if (!document.TryGetValue(RoomDocumentFields.RoomObject.PortalFields.Room, out var roomValue) ||
            !roomValue.IsString) {
            return null;
        }

        var roomName = roomValue.AsString;
        if (string.IsNullOrWhiteSpace(roomName))
            return null;

        var x = ReadCoordinate(document, RoomDocumentFields.RoomObject.PortalFields.X);
        var y = ReadCoordinate(document, RoomDocumentFields.RoomObject.PortalFields.Y);
        var shard = document.TryGetValue(RoomDocumentFields.RoomObject.PortalFields.Shard, out var shardValue) && shardValue.IsString
            ? shardValue.AsString
            : null;

        return new RoomPortalDestinationSnapshot(roomName, x, y, shard);
    }

    private static BsonDocument? MapPortalDestination(RoomPortalDestinationSnapshot? snapshot)
    {
        if (snapshot is null)
            return null;

        var document = new BsonDocument
        {
            [RoomDocumentFields.RoomObject.PortalFields.Room] = snapshot.RoomName,
            [RoomDocumentFields.RoomObject.PortalFields.X] = snapshot.X,
            [RoomDocumentFields.RoomObject.PortalFields.Y] = snapshot.Y
        };

        if (!string.IsNullOrWhiteSpace(snapshot.Shard))
            document[RoomDocumentFields.RoomObject.PortalFields.Shard] = snapshot.Shard;

        return document;
    }

    private static IReadOnlyDictionary<PowerTypes, PowerEffectSnapshot> MapEffects(BsonArray? effects)
    {
        if (effects is null || effects.Count == 0)
            return new Dictionary<PowerTypes, PowerEffectSnapshot>();

        var result = new Dictionary<PowerTypes, PowerEffectSnapshot>(effects.Count);
        for (var i = 0; i < effects.Count; i++) {
            if (effects[i] is not BsonDocument effectDoc)
                continue;

            if (!effectDoc.TryGetValue(RoomDocumentFields.RoomObject.EffectFields.Power, out var powerValue))
                continue;

            if (!effectDoc.TryGetValue(RoomDocumentFields.RoomObject.EffectFields.Level, out var levelValue))
                continue;

            if (!effectDoc.TryGetValue(RoomDocumentFields.RoomObject.EffectFields.EndTime, out var endTimeValue))
                continue;

            var powerType = (PowerTypes)powerValue.AsInt32;
            result[powerType] = new PowerEffectSnapshot(
                Power: powerType,
                Level: levelValue.AsInt32,
                EndTime: endTimeValue.AsInt32
            );
        }

        return result;
    }

    private static TerminalSendSnapshot? MapSend(BsonDocument? send)
    {
        if (send is null || send.ElementCount == 0)
            return null;

        if (!send.TryGetValue(RoomDocumentFields.RoomObject.SendFields.TargetRoomName, out var targetRoomValue) || !targetRoomValue.IsString)
            return null;

        if (!send.TryGetValue(RoomDocumentFields.RoomObject.SendFields.ResourceType, out var resourceValue) || !resourceValue.IsString)
            return null;

        if (!send.TryGetValue(RoomDocumentFields.RoomObject.SendFields.Amount, out var amountValue) || !amountValue.IsInt32)
            return null;

        var targetRoomName = targetRoomValue.AsString;
        var resourceType = resourceValue.AsString;
        var amount = amountValue.AsInt32;

        var description = send.TryGetValue(RoomDocumentFields.RoomObject.SendFields.Description, out var descValue) && descValue.IsString
            ? descValue.AsString
            : null;

        var result = new TerminalSendSnapshot(targetRoomName, resourceType, amount, description);
        return result;
    }

    private static BsonDocument? CreateActionLogDocument(RoomObjectActionLogSnapshot? snapshot)
    {
        if (snapshot is null || !snapshot.HasEntries)
            return null;

        var document = new BsonDocument();
        if (snapshot.Die is { } die) {
            document[RoomDocumentFields.RoomObject.ActionLogFields.Die] = new BsonDocument
            {
                [RoomDocumentFields.RoomObject.ActionLogFields.Time] = die.Time
            };
        }

        if (snapshot.Healed is { } healed) {
            document[RoomDocumentFields.RoomObject.ActionLogFields.Healed] = new BsonDocument
            {
                [RoomDocumentFields.RoomObject.ActionLogFields.X] = healed.X,
                [RoomDocumentFields.RoomObject.ActionLogFields.Y] = healed.Y
            };
        }

        if (snapshot.Repair is { } repair) {
            document[RoomDocumentFields.RoomObject.ActionLogFields.Repair] = new BsonDocument
            {
                [RoomDocumentFields.RoomObject.ActionLogFields.X] = repair.X,
                [RoomDocumentFields.RoomObject.ActionLogFields.Y] = repair.Y
            };
        }

        if (snapshot.Build is { } build) {
            document[RoomDocumentFields.RoomObject.ActionLogFields.Build] = new BsonDocument
            {
                [RoomDocumentFields.RoomObject.ActionLogFields.X] = build.X,
                [RoomDocumentFields.RoomObject.ActionLogFields.Y] = build.Y
            };
        }

        if (snapshot.Harvest is { } harvest) {
            document[RoomDocumentFields.RoomObject.ActionLogFields.Harvest] = new BsonDocument
            {
                [RoomDocumentFields.RoomObject.ActionLogFields.X] = harvest.X,
                [RoomDocumentFields.RoomObject.ActionLogFields.Y] = harvest.Y
            };
        }

        var result = document.ElementCount == 0 ? null : document;
        return result;
    }

    private static RoomObjectActionLogSnapshot? MapActionLog(BsonDocument? actionLog)
    {
        if (actionLog is null || actionLog.ElementCount == 0)
            return null;

        RoomObjectActionLogDie? die = null;
        if (actionLog.TryGetValue(RoomDocumentFields.RoomObject.ActionLogFields.Die, out var dieValue) &&
            dieValue is BsonDocument dieDoc &&
            dieDoc.TryGetValue(RoomDocumentFields.RoomObject.ActionLogFields.Time, out var timeValue) &&
            TryGetInt32(timeValue, out var dieTime)) {
            die = new RoomObjectActionLogDie(dieTime);
        }

        RoomObjectActionLogHealed? healed = null;
        if (actionLog.TryGetValue(RoomDocumentFields.RoomObject.ActionLogFields.Healed, out var healedValue) &&
            healedValue is BsonDocument healedDoc &&
            healedDoc.TryGetValue(RoomDocumentFields.RoomObject.ActionLogFields.X, out var xValue) &&
            healedDoc.TryGetValue(RoomDocumentFields.RoomObject.ActionLogFields.Y, out var yValue) &&
            TryGetInt32(xValue, out var x) &&
            TryGetInt32(yValue, out var y)) {
            healed = new RoomObjectActionLogHealed(x, y);
        }

        RoomObjectActionLogRepair? repair = null;
        if (actionLog.TryGetValue(RoomDocumentFields.RoomObject.ActionLogFields.Repair, out var repairValue) &&
            repairValue is BsonDocument repairDoc &&
            repairDoc.TryGetValue(RoomDocumentFields.RoomObject.ActionLogFields.X, out var repairX) &&
            repairDoc.TryGetValue(RoomDocumentFields.RoomObject.ActionLogFields.Y, out var repairY) &&
            TryGetInt32(repairX, out var repairXInt) &&
            TryGetInt32(repairY, out var repairYInt)) {
            repair = new RoomObjectActionLogRepair(repairXInt, repairYInt);
        }

        RoomObjectActionLogBuild? build = null;
        if (actionLog.TryGetValue(RoomDocumentFields.RoomObject.ActionLogFields.Build, out var buildValue) &&
            buildValue is BsonDocument buildDoc &&
            buildDoc.TryGetValue(RoomDocumentFields.RoomObject.ActionLogFields.X, out var buildX) &&
            buildDoc.TryGetValue(RoomDocumentFields.RoomObject.ActionLogFields.Y, out var buildY) &&
            TryGetInt32(buildX, out var buildXInt) &&
            TryGetInt32(buildY, out var buildYInt)) {
            build = new RoomObjectActionLogBuild(buildXInt, buildYInt);
        }

        RoomObjectActionLogHarvest? harvest = null;
        if (actionLog.GetValue(RoomDocumentFields.RoomObject.ActionLogFields.Harvest, null) is BsonDocument harvestDoc &&
            harvestDoc.TryGetValue(RoomDocumentFields.RoomObject.ActionLogFields.X, out var harvestX) &&
            harvestDoc.TryGetValue(RoomDocumentFields.RoomObject.ActionLogFields.Y, out var harvestY) &&
            TryGetInt32(harvestX, out var harvestXInt) &&
            TryGetInt32(harvestY, out var harvestYInt)) {
            harvest = new RoomObjectActionLogHarvest(harvestXInt, harvestYInt);
        }
        else {
            foreach (var element in actionLog.Elements) {
                if (!string.Equals(element.Name, RoomDocumentFields.RoomObject.ActionLogFields.Harvest, StringComparison.Ordinal))
                    continue;

                if (element.Value is not BsonDocument doc)
                    break;

                if (!doc.TryGetValue(RoomDocumentFields.RoomObject.ActionLogFields.X, out var altX) ||
                    !doc.TryGetValue(RoomDocumentFields.RoomObject.ActionLogFields.Y, out var altY)) {
                    break;
                }

                if (!TryGetInt32(altX, out var altXInt) || !TryGetInt32(altY, out var altYInt))
                    break;

                harvest = new RoomObjectActionLogHarvest(altXInt, altYInt);
                break;
            }
        }

        if (die is null && healed is null && repair is null && build is null && harvest is null)
            return null;

        return new RoomObjectActionLogSnapshot(die, healed, repair, build, harvest);
    }

    private static bool TryGetInt32(BsonValue value, out int result)
    {
        if (value.IsInt32) {
            result = value.AsInt32;
            return true;
        }

        if (value.IsInt64) {
            var temp = value.AsInt64;
            if (temp is >= int.MinValue and <= int.MaxValue) {
                result = (int)temp;
                return true;
            }
        }

        if (value.IsDouble) {
            var temp = value.AsDouble;
            if (!double.IsNaN(temp) && temp is >= int.MinValue and <= int.MaxValue) {
                result = (int)temp;
                return true;
            }
        }

        if (value.IsDecimal128) {
            var dec = value.AsDecimal128;
            if (dec >= int.MinValue && dec <= int.MaxValue) {
                result = (int)dec;
                return true;
            }
        }

        if (value.IsString && int.TryParse(value.AsString, out var parsed)) {
            result = parsed;
            return true;
        }

        result = default;
        return false;
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
            MineralAmount = snapshot.MineralAmount,
            DepositType = snapshot.DepositType,
            StructureType = snapshot.StructureType,
            Store = snapshot.Store.Count == 0 ? null : new Dictionary<string, int>(snapshot.Store, StringComparer.Ordinal),
            StoreCapacity = snapshot.StoreCapacity,
            StoreCapacityResource = new Dictionary<string, int>(snapshot.StoreCapacityResource, StringComparer.Ordinal),
            Energy = snapshot.Energy,
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
            ActionLog = CreateActionLogDocument(snapshot.ActionLog),
            StrongholdId = snapshot.StrongholdId,
            UserSummoned = snapshot.UserSummoned,
            DeathTime = snapshot.DeathTime,
            DecayTime = snapshot.DecayTime,
            CreepId = snapshot.CreepId,
            CreepName = snapshot.CreepName,
            CreepTicksToLive = snapshot.CreepTicksToLive,
            CreepSaying = snapshot.CreepSaying,
            ResourceType = snapshot.ResourceType,
            ResourceAmount = snapshot.ResourceAmount,
            InvaderHarvested = snapshot.InvaderHarvested,
            Progress = snapshot.Progress,
            ProgressTotal = snapshot.ProgressTotal,
            Harvested = snapshot.Harvested,
            Cooldown = snapshot.Cooldown,
            CooldownTime = snapshot.CooldownTime,
            Destination = MapPortalDestination(snapshot.PortalDestination),
            Send = MapSendToBson(snapshot.Send)
        };

        return document;
    }

    private static BsonArray? MapEffectsToBson(IReadOnlyDictionary<PowerTypes, PowerEffectSnapshot>? effects)
    {
        if (effects is null || effects.Count == 0)
            return null;

        var array = new BsonArray(effects.Count);
        foreach (var effect in effects.Values) {
            array.Add(new BsonDocument
            {
                [RoomDocumentFields.RoomObject.EffectFields.Power] = (int)effect.Power,
                [RoomDocumentFields.RoomObject.EffectFields.Level] = effect.Level,
                [RoomDocumentFields.RoomObject.EffectFields.EndTime] = effect.EndTime
            });
        }

        var result = array.Count == 0 ? null : array;
        return result;
    }

    private static BsonDocument? MapSendToBson(TerminalSendSnapshot? send)
    {
        if (send is null)
            return null;

        var document = new BsonDocument
        {
            [RoomDocumentFields.RoomObject.SendFields.TargetRoomName] = send.TargetRoomName,
            [RoomDocumentFields.RoomObject.SendFields.ResourceType] = send.ResourceType,
            [RoomDocumentFields.RoomObject.SendFields.Amount] = send.Amount
        };

        if (send.Description is not null)
            document[RoomDocumentFields.RoomObject.SendFields.Description] = send.Description;

        return document;
    }

    private static RoomSpawnSpawningSnapshot? MapSpawning(BsonValue? spawning)
    {
        if (spawning is null or BsonNull)
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

        if (spawning.Directions is { Count: > 0 }) {
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

    private static int ReadCoordinate(BsonDocument document, string field)
    {
        var value = TryReadInt(document, field) ?? 0;
        if (value < 0) return 0;
        if (value > 49) return 49;
        return value;
    }

    private static IReadOnlyList<Direction> ReadDirections(BsonDocument document, string field)
    {
        if (!document.TryGetValue(field, out var value) || value is not BsonArray array || array.Count == 0)
            return [];

        var result = new List<Direction>(array.Count);
        var seen = new HashSet<Direction>();
        foreach (var element in array) {
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

        var finalResult = result.Count == 0 ? Array.Empty<Direction>() : [.. result];
        return finalResult;
    }

    public static BsonDocument CreateRoomObjectPatchDocument(RoomObjectPatchPayload patch)
    {
        ArgumentNullException.ThrowIfNull(patch);

        var document = new BsonDocument();
        if (patch.Hits.HasValue)
            document[RoomDocumentFields.RoomObject.Hits] = patch.Hits.Value;

        if (patch.Position is { } position) {
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

        if (patch.DecayTime.HasValue)
            document[RoomDocumentFields.RoomObject.DecayTime] = patch.DecayTime.Value;

        if (patch.TicksToLive.HasValue)
            document[RoomDocumentFields.RoomObject.TicksToLive] = patch.TicksToLive.Value;

        if (patch.Progress.HasValue)
            document[RoomDocumentFields.RoomObject.Progress] = patch.Progress.Value;

        if (patch.Energy.HasValue)
            document[RoomDocumentFields.RoomObject.Energy] = patch.Energy.Value;

        if (patch.MineralAmount.HasValue)
            document[RoomDocumentFields.RoomObject.MineralAmount] = patch.MineralAmount.Value;

        if (patch.InvaderHarvested.HasValue)
            document[RoomDocumentFields.RoomObject.InvaderHarvested] = patch.InvaderHarvested.Value;

        if (patch.Harvested.HasValue)
            document[RoomDocumentFields.RoomObject.Harvested] = patch.Harvested.Value;

        if (patch.Cooldown.HasValue)
            document[RoomDocumentFields.RoomObject.Cooldown] = patch.Cooldown.Value;

        if (patch.CooldownTime.HasValue)
            document[RoomDocumentFields.RoomObject.CooldownTime] = patch.CooldownTime.Value;

        if (patch.ActionLog is { } actionLog && actionLog.HasEntries) {
            var logDocument = new BsonDocument();
            if (actionLog.Die is { } die) {
                logDocument[RoomDocumentFields.RoomObject.ActionLogFields.Die] = new BsonDocument
                {
                    [RoomDocumentFields.RoomObject.ActionLogFields.Time] = die.Time
                };
            }

            if (actionLog.Healed is { } healed) {
                logDocument[RoomDocumentFields.RoomObject.ActionLogFields.Healed] = new BsonDocument
                {
                    [RoomDocumentFields.RoomObject.ActionLogFields.X] = healed.X,
                    [RoomDocumentFields.RoomObject.ActionLogFields.Y] = healed.Y
                };
            }

            if (actionLog.Harvest is { } harvest) {
                logDocument[RoomDocumentFields.RoomObject.ActionLogFields.Harvest] = new BsonDocument
                {
                    [RoomDocumentFields.RoomObject.ActionLogFields.X] = harvest.X,
                    [RoomDocumentFields.RoomObject.ActionLogFields.Y] = harvest.Y
                };
            }

            if (logDocument.ElementCount > 0)
                document[RoomDocumentFields.RoomObject.ActionLog] = logDocument;
        }

        if (patch.Store is { Count: > 0 }) {
            foreach (var (resource, amount) in patch.Store) {
                if (string.IsNullOrWhiteSpace(resource))
                    continue;

                var field = $"{RoomDocumentFields.RoomObject.Store.Root}.{resource}";
                document[field] = amount;
            }
        }

        if (patch.StoreCapacity.HasValue)
            document[RoomDocumentFields.RoomObject.Store.Capacity] = patch.StoreCapacity.Value;

        if (patch.StoreCapacityResource is { Count: > 0 }) {
            var capacityDocument = new BsonDocument();
            foreach (var (resourceType, capacity) in patch.StoreCapacityResource) {
                if (string.IsNullOrWhiteSpace(resourceType))
                    continue;

                capacityDocument[resourceType] = capacity;
            }

            if (capacityDocument.ElementCount > 0)
                document[RoomDocumentFields.RoomObject.Store.CapacityResource] = capacityDocument;
        }

        if (patch.Body is { Count: > 0 }) {
            var bodyArray = CreateBodyArray(patch.Body);
            if (bodyArray is not null)
                document[RoomDocumentFields.RoomObject.Body] = bodyArray;
        }

        if (patch.InterRoom is { } interRoom) {
            var destination = new BsonDocument
            {
                [RoomDocumentFields.RoomObject.InterRoomFields.Room] = interRoom.RoomName,
                [RoomDocumentFields.RoomObject.InterRoomFields.X] = interRoom.X,
                [RoomDocumentFields.RoomObject.InterRoomFields.Y] = interRoom.Y
            };

            if (!string.IsNullOrWhiteSpace(interRoom.Shard))
                destination[RoomDocumentFields.RoomObject.InterRoomFields.Shard] = interRoom.Shard;

            document[RoomDocumentFields.RoomObject.InterRoom] = destination;
        }

        if (patch.Spawning is not null) {
            if (MapSpawning(patch.Spawning) is BsonDocument spawningDocument)
                document[RoomDocumentFields.RoomObject.Spawning] = spawningDocument;
        }
        else if (patch.ClearSpawning) {
            document[RoomDocumentFields.RoomObject.Spawning] = BsonNull.Value;
        }

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

        if (patch.ControllerLevel.HasValue) {
            document[RoomDocumentFields.Info.Controller] = new BsonDocument
            {
                [RoomDocumentFields.Info.ControllerLevel] = (int)patch.ControllerLevel.Value
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
        foreach (var part in bodyParts) {
            if (string.IsNullOrWhiteSpace(part.Type))
                continue;

            if (!part.Type.TryParseBodyPartType(out var type))
                continue;

            var hits = part.Hits ?? ScreepsGameConstants.BodyPartHitPoints;
            result.Add(new CreepBodyPartSnapshot(type, hits, string.IsNullOrWhiteSpace(part.Boost) ? null : part.Boost));
        }

        var finalResult = result.Count == 0 ? Array.Empty<CreepBodyPartSnapshot>() : [.. result];
        return finalResult;
    }

    private static List<RoomObjectBodyPartDocument>? MapBodyDocuments(IReadOnlyList<CreepBodyPartSnapshot> body)
    {
        if (body.Count == 0)
            return null;

        var result = new List<RoomObjectBodyPartDocument>(body.Count);
        foreach (var part in body) {
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
        foreach (var part in body) {
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
