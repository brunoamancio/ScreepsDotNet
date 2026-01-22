namespace ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

using System.Text.Json;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.Models;

/// <summary>
/// Loads JSON parity fixtures (shared format with Node.js harness) and converts to RoomState for .NET tests.
/// </summary>
public static class JsonFixtureLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    public static RoomState LoadFromJson(string jsonContent)
    {
        var fixture = JsonSerializer.Deserialize<JsonFixture>(jsonContent, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize JSON fixture");

        return ConvertToRoomState(fixture);
    }

    public static async Task<RoomState> LoadFromFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var jsonContent = await File.ReadAllTextAsync(filePath, cancellationToken);
        return LoadFromJson(jsonContent);
    }

    private static RoomState ConvertToRoomState(JsonFixture fixture)
    {
        var objects = fixture.Objects.ToDictionary(o => o.Id, o => ConvertToRoomObject(o, fixture.Room, fixture.Shard), StringComparer.Ordinal);
        var users = ConvertUsers(fixture.Users);
        var intents = ConvertIntents(fixture.Intents, fixture.Room, fixture.Shard);
        var terrain = ParseTerrain(fixture.Terrain, fixture.Room);

        var roomState = new RoomState(
            RoomName: fixture.Room,
            GameTime: fixture.GameTime,
            Info: null,
            Objects: objects,
            Users: users,
            Intents: intents,
            Terrain: terrain,
            Flags: []);

        return roomState;
    }

    private static RoomObjectSnapshot ConvertToRoomObject(JsonRoomObject obj, string roomName, string shard)
    {
        var store = obj.Store is not null
            ? new Dictionary<string, int>(obj.Store, StringComparer.Ordinal)
            : new Dictionary<string, int>(StringComparer.Ordinal);

        var storeCapacityResource = new Dictionary<string, int>(StringComparer.Ordinal);

        var body = obj.Body is not null ? obj.Body.Select(ConvertBodyPart).ToList() : [];

        var roomObject = new RoomObjectSnapshot(
            Id: obj.Id,
            Type: obj.Type,
            RoomName: roomName,
            Shard: shard,
            UserId: obj.User,
            X: obj.X,
            Y: obj.Y,
            Hits: obj.Hits,
            HitsMax: obj.HitsMax,
            Fatigue: obj.Fatigue,
            TicksToLive: obj.TicksToLive,
            Name: null,
            Level: obj.Level,
            Density: obj.Density,
            MineralType: obj.MineralType,
            DepositType: null,
            StructureType: IsStructure(obj.Type) ? obj.Type : null,
            Store: store,
            StoreCapacity: obj.StoreCapacity,
            StoreCapacityResource: storeCapacityResource,
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Body: body,
            Energy: obj.Energy,
            MineralAmount: obj.MineralAmount,
            Cooldown: obj.Cooldown,
            CooldownTime: obj.CooldownTime,
            Progress: obj.Progress,
            ProgressTotal: obj.ProgressTotal);

        return roomObject;
    }

    private static CreepBodyPartSnapshot ConvertBodyPart(JsonBodyPart bodyPart)
    {
        var bodyPartType = bodyPart.Type switch
        {
            "move" => BodyPartType.Move,
            "work" => BodyPartType.Work,
            "carry" => BodyPartType.Carry,
            "attack" => BodyPartType.Attack,
            "ranged_attack" => BodyPartType.RangedAttack,
            "heal" => BodyPartType.Heal,
            "tough" => BodyPartType.Tough,
            "claim" => BodyPartType.Claim,
            _ => throw new ArgumentException($"Unknown body part type: {bodyPart.Type}")
        };

        var result = new CreepBodyPartSnapshot(bodyPartType, bodyPart.Hits, bodyPart.Boost);
        return result;
    }

    private static IReadOnlyDictionary<string, UserState> ConvertUsers(Dictionary<string, JsonUserState> users)
    {
        var result = new Dictionary<string, UserState>(StringComparer.Ordinal);

        foreach (var (userId, userState) in users) {
            var state = new UserState(
                Id: userId,
                Username: userId,
                Cpu: userState.Cpu,
                Power: userState.Power,
                Money: 0,
                Active: true,
                PowerExperimentationTime: 0,
                Resources: new Dictionary<string, int>(StringComparer.Ordinal));

            result[userId] = state;
        }

        return result;
    }

    private static RoomIntentSnapshot? ConvertIntents(Dictionary<string, Dictionary<string, List<JsonIntent>>> intents, string roomName, string shard)
    {
        if (intents.Count == 0)
            return null;

        var userIntents = new Dictionary<string, IntentEnvelope>(StringComparer.Ordinal);

        foreach (var (userId, objectIntents) in intents) {
            var objectIntentRecords = new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal);

            foreach (var (objectId, intentList) in objectIntents) {
                var records = intentList.Select(ConvertIntent).ToList();
                objectIntentRecords[objectId] = records;
            }

            var envelope = new IntentEnvelope(
                userId,
                objectIntentRecords,
                new Dictionary<string, SpawnIntentEnvelope>(StringComparer.Ordinal),
                new Dictionary<string, CreepIntentEnvelope>(StringComparer.Ordinal));

            userIntents[userId] = envelope;
        }

        var result = new RoomIntentSnapshot(roomName, shard, userIntents);
        return result;
    }

    private static IntentRecord ConvertIntent(JsonIntent intent)
    {
        var fields = new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal);

        if (intent.Id is not null)
            fields[IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: intent.Id);

        if (intent.X.HasValue)
            fields["x"] = new(IntentFieldValueKind.Number, NumberValue: intent.X.Value);

        if (intent.Y.HasValue)
            fields["y"] = new(IntentFieldValueKind.Number, NumberValue: intent.Y.Value);

        if (intent.Amount.HasValue)
            fields[IntentKeys.Amount] = new(IntentFieldValueKind.Number, NumberValue: intent.Amount.Value);

        if (intent.ResourceType is not null)
            fields[IntentKeys.ResourceType] = new(IntentFieldValueKind.Text, TextValue: intent.ResourceType);

        if (intent.Direction.HasValue)
            fields["direction"] = new(IntentFieldValueKind.Number, NumberValue: intent.Direction.Value);

        if (intent.Lab1Id is not null)
            fields["lab1Id"] = new(IntentFieldValueKind.Text, TextValue: intent.Lab1Id);

        if (intent.Lab2Id is not null)
            fields["lab2Id"] = new(IntentFieldValueKind.Text, TextValue: intent.Lab2Id);

        if (intent.BodyPartsCount.HasValue)
            fields["bodyPartsCount"] = new(IntentFieldValueKind.Number, NumberValue: intent.BodyPartsCount.Value);

        var argument = new IntentArgument(fields);
        var record = new IntentRecord(intent.Intent, [argument]);
        return record;
    }

    private static IReadOnlyDictionary<string, RoomTerrainSnapshot> ParseTerrain(string terrain, string roomName)
    {
        if (terrain.Length != 2500)
            throw new ArgumentException($"Terrain string must be exactly 2500 characters (50x50), got {terrain.Length}");

        var result = new Dictionary<string, RoomTerrainSnapshot>(StringComparer.Ordinal)
        {
            [roomName] = new(Id: roomName,
                             RoomName: roomName,
                             Shard: null,
                             Type: "terrain",
                             Terrain: terrain)
        };

        return result;
    }

    private static bool IsStructure(string type)
    {
        var result = type is
            RoomObjectTypes.Extension or
            RoomObjectTypes.Spawn or
            RoomObjectTypes.Link or
            RoomObjectTypes.Storage or
            RoomObjectTypes.Tower or
            RoomObjectTypes.Observer or
            RoomObjectTypes.PowerSpawn or
            RoomObjectTypes.Extractor or
            RoomObjectTypes.Lab or
            RoomObjectTypes.Terminal or
            RoomObjectTypes.Container or
            RoomObjectTypes.Nuker or
            RoomObjectTypes.Factory or
            RoomObjectTypes.Rampart or
            RoomObjectTypes.Road or
            RoomObjectTypes.ConstructedWall;
        return result;
    }
}
