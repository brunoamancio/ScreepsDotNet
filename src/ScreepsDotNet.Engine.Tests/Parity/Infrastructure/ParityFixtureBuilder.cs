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
        Dictionary<string, int>? store = null,
        int? hits = null,
        int? hitsMax = null,
        int fatigue = 0,
        int? ticksToLive = 1500)
    {
        var defaultHits = 100 * bodyParts.Count;
        var actualHits = hits ?? defaultHits;
        var actualHitsMax = hitsMax ?? defaultHits;

        var creep = new RoomObjectSnapshot(
            id,
            RoomObjectTypes.Creep,
            _roomName,
            _shard,
            userId,
            x,
            y,
            Hits: actualHits,
            HitsMax: actualHitsMax,
            Fatigue: fatigue,
            TicksToLive: ticksToLive,
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

    public ParityFixtureBuilder WithAttackIntent(string userId, string creepId, string targetId)
    {
        var argument = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: targetId)
        });

        var record = new IntentRecord(IntentKeys.Attack, [argument]);
        AddIntent(userId, creepId, record);
        return this;
    }

    public ParityFixtureBuilder WithRangedAttackIntent(string userId, string creepId, string targetId)
    {
        var argument = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: targetId)
        });

        var record = new IntentRecord(IntentKeys.RangedAttack, [argument]);
        AddIntent(userId, creepId, record);
        return this;
    }

    public ParityFixtureBuilder WithHealIntent(string userId, string creepId, string targetId)
    {
        var argument = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: targetId)
        });

        var record = new IntentRecord(IntentKeys.Heal, [argument]);
        AddIntent(userId, creepId, record);
        return this;
    }

    public ParityFixtureBuilder WithMoveIntent(string userId, string creepId, Direction direction)
    {
        // Find creep's current position
        var creep = _objects.FirstOrDefault(o => o.Id == creepId)
            ?? throw new InvalidOperationException($"Creep {creepId} not found. Add creep before adding intent.");

        // Calculate destination X,Y from direction
        var (dx, dy) = direction switch
        {
            Direction.Top => (0, -1),
            Direction.TopRight => (1, -1),
            Direction.Right => (1, 0),
            Direction.BottomRight => (1, 1),
            Direction.Bottom => (0, 1),
            Direction.BottomLeft => (-1, 1),
            Direction.Left => (-1, 0),
            Direction.TopLeft => (-1, -1),
            _ => throw new ArgumentException($"Invalid direction: {direction}", nameof(direction))
        };

        var targetX = creep.X + dx;
        var targetY = creep.Y + dy;

        // Create CreepIntentEnvelope with MoveIntent
        if (!_userIntents.TryGetValue(userId, out var envelope)) {
            envelope = new IntentEnvelope(
                userId,
                new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal),
                new Dictionary<string, SpawnIntentEnvelope>(StringComparer.Ordinal),
                new Dictionary<string, CreepIntentEnvelope>(StringComparer.Ordinal)
            );
            _userIntents[userId] = envelope;
        }

        var creepIntents = new Dictionary<string, CreepIntentEnvelope>(envelope.CreepIntents, StringComparer.Ordinal);
        var moveIntent = new MoveIntent(targetX, targetY);
        var creepIntent = new CreepIntentEnvelope(
            Move: moveIntent,
            Attack: null,
            RangedAttack: null,
            AdditionalFields: new Dictionary<string, object?>(StringComparer.Ordinal)
        );
        creepIntents[creepId] = creepIntent;

        _userIntents[userId] = envelope with { CreepIntents = creepIntents };
        return this;
    }

    public ParityFixtureBuilder WithBuildIntent(string userId, string creepId, string targetId)
    {
        var argument = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: targetId)
        });

        var record = new IntentRecord(IntentKeys.Build, [argument]);
        AddIntent(userId, creepId, record);
        return this;
    }

    public ParityFixtureBuilder WithRepairIntent(string userId, string creepId, string targetId)
    {
        var argument = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: targetId)
        });

        var record = new IntentRecord(IntentKeys.Repair, [argument]);
        AddIntent(userId, creepId, record);
        return this;
    }

    public ParityFixtureBuilder WithConstructionSite(string id, int x, int y, string userId, string structureType, int progress, int progressTotal)
    {
        var site = new RoomObjectSnapshot(
            id,
            RoomObjectTypes.ConstructionSite,
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
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: structureType,
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
            ProgressTotal: progressTotal
        );

        _objects.Add(site);
        return this;
    }

    public ParityFixtureBuilder WithRoad(string id, int x, int y, int hits, int hitsMax)
    {
        var road = new RoomObjectSnapshot(
            id,
            RoomObjectTypes.Road,
            _roomName,
            _shard,
            null,
            x,
            y,
            Hits: hits,
            HitsMax: hitsMax,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.Road,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: []
        );

        _objects.Add(road);
        return this;
    }

    public ParityFixtureBuilder WithRampart(string id, int x, int y, string userId, int hits)
    {
        var rampart = new RoomObjectSnapshot(
            id,
            RoomObjectTypes.Rampart,
            _roomName,
            _shard,
            userId,
            x,
            y,
            Hits: hits,
            HitsMax: ScreepsGameConstants.RampartHitsMaxByControllerLevel[ControllerLevel.Level8],
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.Rampart,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: []
        );

        _objects.Add(rampart);
        return this;
    }

    public ParityFixtureBuilder WithSpawn(string id, int x, int y, string userId, int energy)
    {
        var spawn = new RoomObjectSnapshot(
            id,
            RoomObjectTypes.Spawn,
            _roomName,
            _shard,
            userId,
            x,
            y,
            Hits: ScreepsGameConstants.SpawnHits,
            HitsMax: ScreepsGameConstants.SpawnHits,
            Fatigue: null,
            TicksToLive: null,
            Name: id,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.Spawn,
            Store: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [ResourceTypes.Energy] = energy
            },
            StoreCapacity: ScreepsGameConstants.SpawnEnergyCapacity,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [ResourceTypes.Energy] = ScreepsGameConstants.SpawnEnergyCapacity
            },
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: []
        );

        _objects.Add(spawn);
        return this;
    }

    public ParityFixtureBuilder WithRenewIntent(string userId, string spawnId, string targetCreepId)
    {
        if (!_userIntents.TryGetValue(userId, out var envelope)) {
            envelope = new IntentEnvelope(
                userId,
                new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal),
                new Dictionary<string, SpawnIntentEnvelope>(StringComparer.Ordinal),
                new Dictionary<string, CreepIntentEnvelope>(StringComparer.Ordinal)
            );
            _userIntents[userId] = envelope;
        }

        var spawnIntents = new Dictionary<string, SpawnIntentEnvelope>(envelope.SpawnIntents, StringComparer.Ordinal);
        var renewIntent = new RenewCreepIntent(targetCreepId);
        var spawnIntent = new SpawnIntentEnvelope(
            CreateCreep: null,
            RenewCreep: renewIntent,
            RecycleCreep: null,
            SetSpawnDirections: null,
            CancelSpawning: false
        );
        spawnIntents[spawnId] = spawnIntent;

        _userIntents[userId] = envelope with { SpawnIntents = spawnIntents };
        return this;
    }

    public ParityFixtureBuilder WithRecycleIntent(string userId, string spawnId, string targetCreepId)
    {
        if (!_userIntents.TryGetValue(userId, out var envelope)) {
            envelope = new IntentEnvelope(
                userId,
                new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal),
                new Dictionary<string, SpawnIntentEnvelope>(StringComparer.Ordinal),
                new Dictionary<string, CreepIntentEnvelope>(StringComparer.Ordinal)
            );
            _userIntents[userId] = envelope;
        }

        var spawnIntents = new Dictionary<string, SpawnIntentEnvelope>(envelope.SpawnIntents, StringComparer.Ordinal);
        var recycleIntent = new RecycleCreepIntent(targetCreepId);
        var spawnIntent = new SpawnIntentEnvelope(
            CreateCreep: null,
            RenewCreep: null,
            RecycleCreep: recycleIntent,
            SetSpawnDirections: null,
            CancelSpawning: false
        );
        spawnIntents[spawnId] = spawnIntent;

        _userIntents[userId] = envelope with { SpawnIntents = spawnIntents };
        return this;
    }

    public ParityFixtureBuilder WithNuker(string id, int x, int y, string userId, int energy, int ghodium, int? cooldownTime = null)
    {
        var nuker = new RoomObjectSnapshot(
            id,
            RoomObjectTypes.Nuker,
            _roomName,
            _shard,
            userId,
            x,
            y,
            Hits: ScreepsGameConstants.NukerHits,
            HitsMax: ScreepsGameConstants.NukerHits,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.Nuker,
            Store: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [ResourceTypes.Energy] = energy,
                [ResourceTypes.Ghodium] = ghodium
            },
            StoreCapacity: ScreepsGameConstants.NukerEnergyCapacity + ScreepsGameConstants.NukerGhodiumCapacity,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [ResourceTypes.Energy] = ScreepsGameConstants.NukerEnergyCapacity,
                [ResourceTypes.Ghodium] = ScreepsGameConstants.NukerGhodiumCapacity
            },
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: [],
            CooldownTime: cooldownTime
        );

        _objects.Add(nuker);
        return this;
    }

    public ParityFixtureBuilder WithPowerSpawn(string id, int x, int y, string userId, int energy, int power)
    {
        var powerSpawn = new RoomObjectSnapshot(
            id,
            RoomObjectTypes.PowerSpawn,
            _roomName,
            _shard,
            userId,
            x,
            y,
            Hits: ScreepsGameConstants.PowerSpawnHits,
            HitsMax: ScreepsGameConstants.PowerSpawnHits,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.PowerSpawn,
            Store: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [ResourceTypes.Energy] = energy,
                [ResourceTypes.Power] = power
            },
            StoreCapacity: ScreepsGameConstants.PowerSpawnEnergyCapacity + ScreepsGameConstants.PowerSpawnPowerCapacity,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [ResourceTypes.Energy] = ScreepsGameConstants.PowerSpawnEnergyCapacity,
                [ResourceTypes.Power] = ScreepsGameConstants.PowerSpawnPowerCapacity
            },
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: []
        );

        _objects.Add(powerSpawn);
        return this;
    }

    public ParityFixtureBuilder WithFactory(string id, int x, int y, string userId, Dictionary<string, int> store, int? cooldownTime = null, int? level = null)
    {
        var factory = new RoomObjectSnapshot(
            id,
            RoomObjectTypes.Factory,
            _roomName,
            _shard,
            userId,
            x,
            y,
            Hits: ScreepsGameConstants.FactoryHits,
            HitsMax: ScreepsGameConstants.FactoryHits,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: level,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.Factory,
            Store: store,
            StoreCapacity: ScreepsGameConstants.FactoryCapacity,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: [],
            CooldownTime: cooldownTime
        );

        _objects.Add(factory);
        return this;
    }

    public ParityFixtureBuilder WithLaunchNukeIntent(string userId, string nukerId, string targetRoomName, int targetX, int targetY)
    {
        var argument = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [NukerIntentFields.RoomName] = new(IntentFieldValueKind.Text, TextValue: targetRoomName),
            [NukerIntentFields.X] = new(IntentFieldValueKind.Number, NumberValue: targetX),
            [NukerIntentFields.Y] = new(IntentFieldValueKind.Number, NumberValue: targetY)
        });

        var record = new IntentRecord(IntentKeys.LaunchNuke, [argument]);
        AddIntent(userId, nukerId, record);
        return this;
    }

    public ParityFixtureBuilder WithProcessPowerIntent(string userId, string powerSpawnId)
    {
        var argument = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal));
        var record = new IntentRecord(IntentKeys.ProcessPower, [argument]);
        AddIntent(userId, powerSpawnId, record);
        return this;
    }

    public ParityFixtureBuilder WithProduceIntent(string userId, string factoryId, string resourceType)
    {
        var argument = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.ResourceType] = new(IntentFieldValueKind.Text, TextValue: resourceType)
        });

        var record = new IntentRecord(IntentKeys.Produce, [argument]);
        AddIntent(userId, factoryId, record);
        return this;
    }

    private void AddIntent(string userId, string objectId, IntentRecord record)
    {
        if (!_userIntents.TryGetValue(userId, out var envelope)) {
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
