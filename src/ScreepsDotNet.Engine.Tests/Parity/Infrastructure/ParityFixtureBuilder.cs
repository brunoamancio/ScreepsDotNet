namespace ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.Models;

/// <summary>
/// Fluent builder for creating parity test fixtures programmatically
/// Simplifies fixture creation without requiring JSON files
/// </summary>
public sealed class ParityFixtureBuilder
{
    private readonly List<RoomObjectSnapshot> _objects = [];
    private readonly Dictionary<string, IntentEnvelope> _userIntents = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RoomTerrainSnapshot> _terrain = new(StringComparer.Ordinal);
    private string _roomName = "W1N1";
    private string _shard = "shard0";
    private int _gameTime = 100;

    public ParityFixtureBuilder WithRoom(string roomName)
    {
        _roomName = roomName;
        return this;
    }

    public ParityFixtureBuilder WithShard(string shard)
    {
        _shard = shard;
        return this;
    }

    public ParityFixtureBuilder WithGameTime(int gameTime)
    {
        _gameTime = gameTime;
        return this;
    }

    public ParityFixtureBuilder WithCreep(
        string id,
        int x,
        int y,
        string userId,
        IReadOnlyList<BodyPartType> bodyParts,
        int capacity = 50,
        Dictionary<string, int>? store = null)
    {
        var creep = new RoomObjectSnapshot(
            id,
            RoomObjectTypes.Creep,
            _roomName,
            _shard,
            userId,
            x,
            y,
            Hits: 100 * bodyParts.Count,
            HitsMax: 100 * bodyParts.Count,
            Fatigue: 0,
            TicksToLive: 1500,
            Name: id,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: null,
            Store: store ?? new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: capacity,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: bodyParts.Select(type => new CreepBodyPartSnapshot(type, ScreepsGameConstants.BodyPartHitPoints, null)).ToArray()
        );

        _objects.Add(creep);
        return this;
    }

    public ParityFixtureBuilder WithSource(string id, int x, int y, int energy = 3000)
    {
        var source = new RoomObjectSnapshot(
            id,
            RoomObjectTypes.Source,
            _roomName,
            _shard,
            null,
            x,
            y,
            Hits: null,
            HitsMax: null,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.Source,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: [],
            Energy: energy,
            InvaderHarvested: 0
        );

        _objects.Add(source);
        return this;
    }

    public ParityFixtureBuilder WithController(string id, int x, int y, string userId, int level = 1, int progress = 0)
    {
        var controller = new RoomObjectSnapshot(
            id,
            RoomObjectTypes.Controller,
            _roomName,
            _shard,
            userId,
            x,
            y,
            Hits: null,
            HitsMax: null,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: level,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.Controller,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: [],
            Progress: progress,
            ProgressTotal: ScreepsGameConstants.ControllerLevelProgress[(ControllerLevel)level]
        );

        _objects.Add(controller);
        return this;
    }

    public ParityFixtureBuilder WithLink(string id, int x, int y, string userId, int energy = 0, int? cooldown = null, int? cooldownTime = null)
    {
        var link = new RoomObjectSnapshot(
            id,
            RoomObjectTypes.Link,
            _roomName,
            _shard,
            userId,
            x,
            y,
            Hits: ScreepsGameConstants.LinkHits,
            HitsMax: ScreepsGameConstants.LinkHitsMax,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.Link,
            Store: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [ResourceTypes.Energy] = energy
            },
            StoreCapacity: ScreepsGameConstants.LinkCapacity,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [ResourceTypes.Energy] = ScreepsGameConstants.LinkCapacity
            },
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: [],
            Cooldown: cooldown,
            CooldownTime: cooldownTime
        );

        _objects.Add(link);
        return this;
    }

    public ParityFixtureBuilder WithHarvestIntent(string userId, string creepId, string targetId)
    {
        var argument = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: targetId)
        });

        var record = new IntentRecord(IntentKeys.Harvest, [argument]);
        AddIntent(userId, creepId, record);
        return this;
    }

    public ParityFixtureBuilder WithUpgradeIntent(string userId, string creepId, string controllerId)
    {
        var argument = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: controllerId)
        });

        var record = new IntentRecord(IntentKeys.UpgradeController, [argument]);
        AddIntent(userId, creepId, record);
        return this;
    }

    public ParityFixtureBuilder WithTransferIntent(string userId, string creepId, string targetId, string resourceType, int amount)
    {
        var argument = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: targetId),
            [IntentKeys.ResourceType] = new(IntentFieldValueKind.Text, TextValue: resourceType),
            [IntentKeys.Amount] = new(IntentFieldValueKind.Number, NumberValue: amount)
        });

        var record = new IntentRecord(IntentKeys.Transfer, [argument]);
        AddIntent(userId, creepId, record);
        return this;
    }

    public ParityFixtureBuilder WithTransferEnergyIntent(string userId, string sourceLinkId, string targetLinkId, int amount)
    {
        var argument = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: targetLinkId),
            [IntentKeys.Amount] = new(IntentFieldValueKind.Number, NumberValue: amount)
        });

        var record = new IntentRecord(IntentKeys.TransferEnergy, [argument]);
        AddIntent(userId, sourceLinkId, record);
        return this;
    }

    public ParityFixtureBuilder WithLab(string id, int x, int y, string userId, Dictionary<string, int> store, int? cooldownTime = null)
    {
        var lab = new RoomObjectSnapshot(
            id,
            RoomObjectTypes.Lab,
            _roomName,
            _shard,
            userId,
            x,
            y,
            Hits: 500,
            HitsMax: 500,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.Lab,
            Store: store,
            StoreCapacity: ScreepsGameConstants.LabEnergyCapacity + ScreepsGameConstants.LabMineralCapacity,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: [],
            CooldownTime: cooldownTime
        );

        _objects.Add(lab);
        return this;
    }

    public ParityFixtureBuilder WithRunReactionIntent(string userId, string outputLabId, string inputLab1Id, string inputLab2Id)
    {
        var argument = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.Lab1] = new(IntentFieldValueKind.Text, TextValue: inputLab1Id),
            [IntentKeys.Lab2] = new(IntentFieldValueKind.Text, TextValue: inputLab2Id)
        });

        var record = new IntentRecord(IntentKeys.RunReaction, [argument]);
        AddIntent(userId, outputLabId, record);
        return this;
    }

    public ParityFixtureBuilder WithBoostCreepIntent(string userId, string labId, string creepId, int? bodyPartsCount = null)
    {
        var fields = new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: creepId)
        };

        if (bodyPartsCount.HasValue)
            fields[IntentKeys.BodyPartsCount] = new(IntentFieldValueKind.Number, NumberValue: bodyPartsCount.Value);

        var argument = new IntentArgument(fields);
        var record = new IntentRecord(IntentKeys.BoostCreep, [argument]);
        AddIntent(userId, labId, record);
        return this;
    }

    private void AddIntent(string userId, string objectId, IntentRecord record)
    {
        if (!_userIntents.TryGetValue(userId, out var envelope))
        {
            envelope = new IntentEnvelope(
                userId,
                new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal),
                new Dictionary<string, SpawnIntentEnvelope>(StringComparer.Ordinal),
                new Dictionary<string, CreepIntentEnvelope>(StringComparer.Ordinal)
            );
            _userIntents[userId] = envelope;
        }

        var objectIntents = new Dictionary<string, IReadOnlyList<IntentRecord>>(envelope.ObjectIntents, StringComparer.Ordinal);
        var existingIntents = objectIntents.GetValueOrDefault(objectId, []).ToList();
        existingIntents.Add(record);
        objectIntents[objectId] = existingIntents;

        _userIntents[userId] = envelope with { ObjectIntents = objectIntents };
    }

    public RoomState Build()
    {
        var objects = _objects.ToDictionary(o => o.Id, o => o, StringComparer.Ordinal);
        var intents = _userIntents.Count > 0
            ? new RoomIntentSnapshot(_roomName, _shard, _userIntents)
            : null;

        return new RoomState(
            _roomName,
            _gameTime,
            null,
            objects,
            new Dictionary<string, UserState>(StringComparer.Ordinal),
            intents,
            _terrain,
            []
        );
    }
}
