namespace ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

using System.Text.Json;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Extensions;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.Models;

/// <summary>
/// Loads multi-room JSON parity fixtures and converts to GlobalState for .NET Engine tests.
/// Supports testing cross-room operations like Terminal.send, observer.observeRoom, etc.
/// </summary>
public static class MultiRoomFixtureLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    public static GlobalState LoadFromJson(string jsonContent)
    {
        var fixture = JsonSerializer.Deserialize<JsonMultiRoomFixture>(jsonContent, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize multi-room JSON fixture");

        return ConvertToGlobalState(fixture);
    }

    public static async Task<GlobalState> LoadFromFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var jsonContent = await File.ReadAllTextAsync(filePath, cancellationToken);
        return LoadFromJson(jsonContent);
    }

    private static GlobalState ConvertToGlobalState(JsonMultiRoomFixture fixture)
    {
        // Build AccessibleRooms dictionary (one RoomInfoSnapshot per room)
        var accessibleRooms = BuildAccessibleRooms(fixture.Rooms, fixture.Shard);

        // Flatten all room objects and extract special objects (terminals, observers, power creeps)
        var allObjects = new Dictionary<string, RoomObjectSnapshot>(StringComparer.Ordinal);
        var specialObjects = new List<RoomObjectSnapshot>();

        foreach (var (roomName, roomData) in fixture.Rooms) {
            foreach (var jsonObj in roomData.Objects) {
                var roomObject = ConvertToRoomObject(jsonObj, roomName, fixture.Shard, fixture.GameTime);
                allObjects[roomObject.Id] = roomObject;

                // Extract special room objects (terminals, observers, power creeps, etc.)
                if (IsSpecialRoomObject(roomObject.Type))
                    specialObjects.Add(roomObject);
            }
        }

        // Convert intents per room
        var roomIntents = ConvertRoomIntents(fixture.Intents, allObjects, fixture.Shard);

        // Build GlobalMarketSnapshot (empty orders, but include users)
        var users = ConvertUsers(fixture.Users);
        var market = new GlobalMarketSnapshot(
            Orders: [],
            Users: users,
            PowerCreeps: [],
            UserIntents: [],
            ShardName: fixture.Shard);

        // Build GlobalState
        var globalState = new GlobalState(
            GameTime: fixture.GameTime,
            MovingCreeps: [],  // No inter-room creep movement in fixtures
            AccessibleRooms: accessibleRooms,
            ExitTopology: new Dictionary<string, RoomExitTopology>(StringComparer.Ordinal),  // Terminal.send doesn't need exit topology
            SpecialRoomObjects: specialObjects,
            Market: market,
            RoomIntents: roomIntents);

        return globalState;
    }

    private static IReadOnlyDictionary<string, RoomInfoSnapshot> BuildAccessibleRooms(Dictionary<string, JsonRoomData> rooms, string shard)
    {
        var result = new Dictionary<string, RoomInfoSnapshot>(StringComparer.Ordinal);

        foreach (var (roomName, _) in rooms) {
            var roomInfo = new RoomInfoSnapshot(
                RoomName: roomName,
                Shard: shard,
                Status: "normal",
                IsNoviceArea: false,
                IsRespawnArea: false,
                OpenTime: null,
                OwnerUserId: null,
                ControllerLevel: null,
                EnergyAvailable: null,
                NextNpcMarketOrder: null,
                PowerBankTime: null,
                InvaderGoal: null,
                Type: RoomType.Normal);

            result[roomName] = roomInfo;
        }

        return result;
    }

    private static RoomObjectSnapshot ConvertToRoomObject(JsonRoomObject obj, string roomName, string shard, int gameTime)
    {
        var store = obj.Store is not null
            ? new Dictionary<string, int>(obj.Store, StringComparer.Ordinal)
            : new Dictionary<string, int>(StringComparer.Ordinal);

        var storeCapacityResource = new Dictionary<string, int>(StringComparer.Ordinal);
        if (obj.EnergyCapacity.HasValue) {
            storeCapacityResource[ResourceTypes.Energy] = obj.EnergyCapacity.Value;
        }

        var body = obj.Body is not null ? obj.Body.Select(ConvertBodyPart).ToList() : [];

        var nextRegenerationTime = obj.TicksToRegeneration.HasValue
            ? (int?)(gameTime + obj.TicksToRegeneration.Value)
            : null;

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
            StructureType: obj.StructureType ?? (IsStructure(obj.Type) ? obj.Type : null),
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
            DecayTime: obj.NextDecayTime,
            NextRegenerationTime: nextRegenerationTime,
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

        var hits = bodyPart.Hits ?? ScreepsGameConstants.BodyPartHitPoints;
        var result = new CreepBodyPartSnapshot(bodyPartType, hits, bodyPart.Boost);
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

    private static IReadOnlyDictionary<string, RoomIntentSnapshot> ConvertRoomIntents(Dictionary<string, Dictionary<string, Dictionary<string, List<JsonIntent>>>> intentsPerRoom, Dictionary<string, RoomObjectSnapshot> allObjects, string shard)
    {
        var result = new Dictionary<string, RoomIntentSnapshot>(StringComparer.Ordinal);

        foreach (var (roomName, userIntents) in intentsPerRoom) {
            var roomObjectMap = allObjects.Where(kv => kv.Value.RoomName == roomName).ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
            var intentSnapshot = ConvertIntentsForRoom(roomName, userIntents, roomObjectMap, shard);
            if (intentSnapshot is not null)
                result[roomName] = intentSnapshot;
        }

        return result;
    }

    private static RoomIntentSnapshot? ConvertIntentsForRoom(string roomName, Dictionary<string, Dictionary<string, List<JsonIntent>>> userIntents, Dictionary<string, RoomObjectSnapshot> roomObjects, string shard)
    {
        if (userIntents.Count == 0)
            return null;

        var users = new Dictionary<string, IntentEnvelope>(StringComparer.Ordinal);

        foreach (var (userId, objectIntents) in userIntents) {
            var objectIntentRecords = new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal);
            var creepIntents = new Dictionary<string, CreepIntentEnvelope>(StringComparer.Ordinal);
            var terminalIntents = new Dictionary<string, TerminalIntentEnvelope>(StringComparer.Ordinal);

            foreach (var (objectId, intentList) in objectIntents) {
                // Separate creep, terminal, and other intents
                var creepSpecificIntents = new List<JsonIntent>();
                var terminalSpecificIntents = new List<JsonIntent>();
                var otherIntents = new List<JsonIntent>();

                foreach (var intent in intentList) {
                    if (intent.Intent is IntentKeys.Move or IntentKeys.Attack or IntentKeys.RangedAttack or IntentKeys.RangedMassAttack or IntentKeys.Heal or IntentKeys.RangedHeal)
                        creepSpecificIntents.Add(intent);
                    else if (intent.Intent is IntentKeys.Send)
                        terminalSpecificIntents.Add(intent);
                    else
                        otherIntents.Add(intent);
                }

                // Map creep-specific intents to CreepIntentEnvelope
                if (creepSpecificIntents.Count > 0) {
                    MoveIntent? moveIntent = null;
                    AttackIntent? attackIntent = null;
                    AttackIntent? rangedAttackIntent = null;
                    var rangedMassAttack = false;
                    HealIntent? healIntent = null;
                    HealIntent? rangedHealIntent = null;

                    foreach (var intent in creepSpecificIntents) {
                        switch (intent.Intent) {
                            case IntentKeys.Move:
                                if (intent.Direction.HasValue && roomObjects.TryGetValue(objectId, out var creep)) {
                                    var (dx, dy) = DirectionExtensions.ToOffset(intent.Direction.Value);
                                    moveIntent = new MoveIntent(creep.X + dx, creep.Y + dy);
                                }
                                break;
                            case IntentKeys.Attack:
                                attackIntent = new AttackIntent(intent.Id!, null);
                                break;
                            case IntentKeys.RangedAttack:
                                rangedAttackIntent = new AttackIntent(intent.Id!, null);
                                break;
                            case IntentKeys.RangedMassAttack:
                                rangedMassAttack = true;
                                break;
                            case IntentKeys.Heal:
                                healIntent = new HealIntent(intent.Id!, null);
                                break;
                            case IntentKeys.RangedHeal:
                                rangedHealIntent = new HealIntent(intent.Id!, null);
                                break;
                        }
                    }

                    creepIntents[objectId] = new CreepIntentEnvelope(
                        Move: moveIntent,
                        Attack: attackIntent,
                        RangedAttack: rangedAttackIntent,
                        RangedMassAttack: rangedMassAttack,
                        Heal: healIntent,
                        RangedHeal: rangedHealIntent,
                        AdditionalFields: new Dictionary<string, object?>(StringComparer.Ordinal));
                }

                // Map terminal-specific intents to TerminalIntentEnvelope
                if (terminalSpecificIntents.Count > 0) {
                    TerminalSendIntent? sendIntent = null;

                    foreach (var intent in terminalSpecificIntents) {
                        if (intent.Intent == IntentKeys.Send && intent.TargetRoomName is not null && intent.ResourceType is not null && intent.Amount.HasValue) {
                            sendIntent = new TerminalSendIntent(
                                intent.TargetRoomName,
                                intent.ResourceType,
                                intent.Amount.Value,
                                intent.Description);
                        }
                    }

                    terminalIntents[objectId] = new TerminalIntentEnvelope(sendIntent);
                }

                // Keep other intents in ObjectIntents
                if (otherIntents.Count > 0) {
                    var records = otherIntents.Select(ConvertIntent).ToList();
                    objectIntentRecords[objectId] = records;
                }
            }

            var envelope = new IntentEnvelope(
                userId,
                objectIntentRecords,
                new Dictionary<string, SpawnIntentEnvelope>(StringComparer.Ordinal),
                creepIntents,
                terminalIntents);

            users[userId] = envelope;
        }

        var result = new RoomIntentSnapshot(roomName, shard, users);
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

        if (intent.TargetRoomName is not null)
            fields["targetRoomName"] = new(IntentFieldValueKind.Text, TextValue: intent.TargetRoomName);

        if (intent.Description is not null)
            fields["description"] = new(IntentFieldValueKind.Text, TextValue: intent.Description);

        var argument = new IntentArgument(fields);
        var record = new IntentRecord(intent.Intent, [argument]);
        return record;
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

    private static bool IsSpecialRoomObject(string type)
    {
        // Special room objects that need to be in GlobalState.SpecialRoomObjects
        var result = type is
            RoomObjectTypes.Terminal or
            RoomObjectTypes.Observer or
            RoomObjectTypes.PowerBank or
            RoomObjectTypes.PowerSpawn;
        return result;
    }
}
