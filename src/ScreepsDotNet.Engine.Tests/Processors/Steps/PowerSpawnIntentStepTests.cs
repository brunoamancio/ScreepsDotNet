namespace ScreepsDotNet.Engine.Tests.Processors.Steps;
using ScreepsDotNet.Engine.Tests.Processors.Helpers;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.Bulk;
using ScreepsDotNet.Engine.Data.Models;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.Helpers;
using ScreepsDotNet.Engine.Processors.Steps;

public sealed class PowerSpawnIntentStepTests
{
    private readonly PowerSpawnIntentStep _step = new();

    [Fact]
    public async Task BasicProcessing_ConsumesPowerAndEnergy()
    {
        // Arrange
        var powerSpawn = CreatePowerSpawn("ps1", 10, 10, "user1", power: 10, energy: 500);
        var context = CreateContext([powerSpawn],
            CreateProcessPowerIntent("user1", powerSpawn.Id), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == powerSpawn.Id);
        Assert.Equal(9, Payload.Store![ResourceTypes.Power]);
        Assert.Equal(450, Payload.Store![ResourceTypes.Energy]);
    }

    [Fact]
    public async Task InsufficientPower_DoesNothing()
    {
        // Arrange
        var powerSpawn = CreatePowerSpawn("ps1", 10, 10, "user1", power: 0, energy: 500);
        var context = CreateContext([powerSpawn],
            CreateProcessPowerIntent("user1", powerSpawn.Id), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task InsufficientEnergy_DoesNothing()
    {
        // Arrange
        var powerSpawn = CreatePowerSpawn("ps1", 10, 10, "user1", power: 10, energy: 30);
        var context = CreateContext([powerSpawn],
            CreateProcessPowerIntent("user1", powerSpawn.Id), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task NotPowerSpawn_DoesNothing()
    {
        // Arrange
        var spawn = CreateSpawn("spawn1", 10, 10, "user1");
        var context = CreateContext([spawn],
            CreateProcessPowerIntent("user1", spawn.Id), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task NoStore_DoesNothing()
    {
        // Arrange
        var powerSpawn = CreatePowerSpawnNoStore("ps1", 10, 10, "user1");
        var context = CreateContext([powerSpawn],
            CreateProcessPowerIntent("user1", powerSpawn.Id), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task MultipleProcessingCalls_ConsumesCumulatively()
    {
        // Arrange
        var powerSpawn = CreatePowerSpawn("ps1", 10, 10, "user1", power: 10, energy: 500);
        var context = CreateContext([powerSpawn],
            CreateMultipleProcessPowerIntents("user1", powerSpawn.Id, count: 3), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == powerSpawn.Id);
        Assert.Equal(7, Payload.Store![ResourceTypes.Power]);
        Assert.Equal(350, Payload.Store![ResourceTypes.Energy]);
    }

    [Fact]
    public async Task WithOperatePowerEffectLevel1_ProcessesBoostedAmount()
    {
        // Arrange
        var powerSpawn = CreatePowerSpawnWithEffect("ps1", 10, 10, "user1", power: 10, energy: 500,
            PowerTypes.OperatePower, level: 1, endTime: 200);
        var context = CreateContext([powerSpawn],
            CreateProcessPowerIntent("user1", powerSpawn.Id), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - level 1 effect adds +1 bonus = 2 power processed
        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == powerSpawn.Id);
        var result = Payload.Store!;
        var expectedPower = 10 - 2;
        var expectedEnergy = 500 - (2 * ScreepsGameConstants.PowerSpawnEnergyRatio);
        Assert.Equal(expectedPower, result[ResourceTypes.Power]);
        Assert.Equal(expectedEnergy, result[ResourceTypes.Energy]);
    }

    [Fact]
    public async Task WithOperatePowerEffectLevel3_ProcessesBoostedAmount()
    {
        // Arrange
        var powerSpawn = CreatePowerSpawnWithEffect("ps1", 10, 10, "user1", power: 10, energy: 500,
            PowerTypes.OperatePower, level: 3, endTime: 200);
        var context = CreateContext([powerSpawn],
            CreateProcessPowerIntent("user1", powerSpawn.Id), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - level 3 effect adds +3 bonus = 4 power processed
        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == powerSpawn.Id);
        var result = Payload.Store!;
        var expectedPower = 10 - 4;
        var expectedEnergy = 500 - (4 * ScreepsGameConstants.PowerSpawnEnergyRatio);
        Assert.Equal(expectedPower, result[ResourceTypes.Power]);
        Assert.Equal(expectedEnergy, result[ResourceTypes.Energy]);
    }

    [Fact]
    public async Task WithOperatePowerEffectLevel5_ProcessesBoostedAmount()
    {
        // Arrange
        var powerSpawn = CreatePowerSpawnWithEffect("ps1", 10, 10, "user1", power: 10, energy: 500,
            PowerTypes.OperatePower, level: 5, endTime: 200);
        var context = CreateContext([powerSpawn],
            CreateProcessPowerIntent("user1", powerSpawn.Id), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - level 5 effect adds +5 bonus = 6 power processed
        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == powerSpawn.Id);
        var result = Payload.Store!;
        var expectedPower = 10 - 6;
        var expectedEnergy = 500 - (6 * ScreepsGameConstants.PowerSpawnEnergyRatio);
        Assert.Equal(expectedPower, result[ResourceTypes.Power]);
        Assert.Equal(expectedEnergy, result[ResourceTypes.Energy]);
    }

    #region Helper Methods

    private static RoomObjectSnapshot CreatePowerSpawn(string id, int x, int y, string userId, int power, int energy)
        => new(
            id,
            RoomObjectTypes.PowerSpawn,
            "W1N1",
            "shard0",
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
                [ResourceTypes.Power] = power,
                [ResourceTypes.Energy] = energy
            },
            StoreCapacity: ScreepsGameConstants.PowerSpawnEnergyCapacity + ScreepsGameConstants.PowerSpawnPowerCapacity,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [ResourceTypes.Power] = ScreepsGameConstants.PowerSpawnPowerCapacity,
                [ResourceTypes.Energy] = ScreepsGameConstants.PowerSpawnEnergyCapacity
            },
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: [],
            IsSpawning: null,
            UserSummoned: null,
            IsPublic: null,
            StrongholdId: null,
            DeathTime: null,
            DecayTime: null,
            CreepId: null,
            CreepName: null,
            CreepTicksToLive: null,
            CreepSaying: null,
            ResourceType: null,
            ResourceAmount: null,
            Progress: null,
            ProgressTotal: null,
            ActionLog: null,
            Energy: null,
            MineralAmount: null,
            InvaderHarvested: null,
            Harvested: null,
            Cooldown: null,
            CooldownTime: null,
            SafeMode: null,
            SafeModeAvailable: null,
            PortalDestination: null,
            Send: null);

    private static RoomObjectSnapshot CreatePowerSpawnWithEffect(string id, int x, int y, string userId, int power, int energy, PowerTypes powerType, int level, int endTime)
        => new(
            id,
            RoomObjectTypes.PowerSpawn,
            "W1N1",
            "shard0",
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
                [ResourceTypes.Power] = power,
                [ResourceTypes.Energy] = energy
            },
            StoreCapacity: ScreepsGameConstants.PowerSpawnEnergyCapacity + ScreepsGameConstants.PowerSpawnPowerCapacity,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [ResourceTypes.Power] = ScreepsGameConstants.PowerSpawnPowerCapacity,
                [ResourceTypes.Energy] = ScreepsGameConstants.PowerSpawnEnergyCapacity
            },
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>()
            {
                [powerType] = new(powerType, level, endTime)
            },
            Spawning: null,
            Body: [],
            IsSpawning: null,
            UserSummoned: null,
            IsPublic: null,
            StrongholdId: null,
            DeathTime: null,
            DecayTime: null,
            CreepId: null,
            CreepName: null,
            CreepTicksToLive: null,
            CreepSaying: null,
            ResourceType: null,
            ResourceAmount: null,
            Progress: null,
            ProgressTotal: null,
            ActionLog: null,
            Energy: null,
            MineralAmount: null,
            InvaderHarvested: null,
            Harvested: null,
            Cooldown: null,
            CooldownTime: null,
            SafeMode: null,
            SafeModeAvailable: null,
            PortalDestination: null,
            Send: null);

    private static RoomObjectSnapshot CreatePowerSpawnNoStore(string id, int x, int y, string userId)
        => new(
            id,
            RoomObjectTypes.PowerSpawn,
            "W1N1",
            "shard0",
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
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: ScreepsGameConstants.PowerSpawnEnergyCapacity + ScreepsGameConstants.PowerSpawnPowerCapacity,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: [],
            IsSpawning: null,
            UserSummoned: null,
            IsPublic: null,
            StrongholdId: null,
            DeathTime: null,
            DecayTime: null,
            CreepId: null,
            CreepName: null,
            CreepTicksToLive: null,
            CreepSaying: null,
            ResourceType: null,
            ResourceAmount: null,
            Progress: null,
            ProgressTotal: null,
            ActionLog: null,
            Energy: null,
            MineralAmount: null,
            InvaderHarvested: null,
            Harvested: null,
            Cooldown: null,
            CooldownTime: null,
            SafeMode: null,
            SafeModeAvailable: null,
            PortalDestination: null,
            Send: null);

    private static RoomObjectSnapshot CreateSpawn(string id, int x, int y, string userId)
        => new(
            id,
            RoomObjectTypes.Spawn,
            "W1N1",
            "shard0",
            userId,
            x,
            y,
            Hits: 5000,
            HitsMax: 5000,
            Fatigue: null,
            TicksToLive: null,
            Name: "Spawn1",
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.Spawn,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: 300,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: [],
            IsSpawning: null,
            UserSummoned: null,
            IsPublic: null,
            StrongholdId: null,
            DeathTime: null,
            DecayTime: null,
            CreepId: null,
            CreepName: null,
            CreepTicksToLive: null,
            CreepSaying: null,
            ResourceType: null,
            ResourceAmount: null,
            Progress: null,
            ProgressTotal: null,
            ActionLog: null,
            Energy: null,
            MineralAmount: null,
            InvaderHarvested: null,
            Harvested: null,
            Cooldown: null,
            CooldownTime: null,
            SafeMode: null,
            SafeModeAvailable: null,
            PortalDestination: null,
            Send: null);

    private static RoomIntentSnapshot CreateProcessPowerIntent(string userId, string powerSpawnId)
    {
        var fields = new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal);
        var argument = new IntentArgument(fields);
        var record = new IntentRecord(IntentKeys.ProcessPower, [argument]);
        var objectIntents = new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal)
        {
            [powerSpawnId] = [record]
        };

        var envelope = new IntentEnvelope(
            userId,
            objectIntents,
            new Dictionary<string, SpawnIntentEnvelope>(StringComparer.Ordinal),
            new Dictionary<string, CreepIntentEnvelope>(StringComparer.Ordinal));

        var users = new Dictionary<string, IntentEnvelope>(StringComparer.Ordinal)
        {
            [userId] = envelope
        };

        var result = new RoomIntentSnapshot("W1N1", "shard0", users);
        return result;
    }

    private static RoomIntentSnapshot CreateMultipleProcessPowerIntents(string userId, string powerSpawnId, int count)
    {
        var fields = new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal);
        var argument = new IntentArgument(fields);
        var records = Enumerable.Range(0, count)
            .Select(_ => new IntentRecord(IntentKeys.ProcessPower, [argument]))
            .ToList();

        var objectIntents = new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal)
        {
            [powerSpawnId] = records
        };

        var envelope = new IntentEnvelope(
            userId,
            objectIntents,
            new Dictionary<string, SpawnIntentEnvelope>(StringComparer.Ordinal),
            new Dictionary<string, CreepIntentEnvelope>(StringComparer.Ordinal));

        var users = new Dictionary<string, IntentEnvelope>(StringComparer.Ordinal)
        {
            [userId] = envelope
        };

        var result = new RoomIntentSnapshot("W1N1", "shard0", users);
        return result;
    }

    private static RoomProcessorContext CreateContext(IEnumerable<RoomObjectSnapshot> objects, RoomIntentSnapshot? intents = null, int gameTime = 100)
    {
        var objectMap = objects.ToDictionary(o => o.Id, o => o, StringComparer.Ordinal);

        var state = new RoomState(
            "W1N1",
            gameTime,
            null,
            objectMap,
            new Dictionary<string, UserState>(StringComparer.Ordinal),
            intents,
            new Dictionary<string, RoomTerrainSnapshot>(StringComparer.Ordinal),
            []);

        return new RoomProcessorContext(state, new FakeMutationWriter(), new FakeCreepStatsSink(), new NullGlobalMutationWriter());
    }

    private sealed class FakeMutationWriter : IRoomMutationWriter
    {
        public List<(string ObjectId, RoomObjectPatchPayload Payload)> Patches { get; } = [];
        public List<RoomObjectSnapshot> Upserts { get; } = [];
        public List<string> Removals { get; } = [];
        public RoomInfoPatchPayload? RoomInfoPatch { get; private set; }

        public void Upsert(RoomObjectSnapshot document) => Upserts.Add(document);

        public void Patch(string objectId, RoomObjectPatchPayload patch) => Patches.Add((objectId, patch));

        public void Remove(string objectId) => Removals.Add(objectId);

        public void SetRoomInfoPatch(RoomInfoPatchPayload patch) => RoomInfoPatch = patch;

        public void SetEventLog(IRoomEventLogPayload? eventLog) { }

        public void SetMapView(IRoomMapViewPayload? mapView) { }

        public Task FlushAsync(CancellationToken token = default) => Task.CompletedTask;

        public void Reset()
        {
            Patches.Clear();
            Upserts.Clear();
            Removals.Clear();
        }
    }

    private sealed class FakeCreepStatsSink : ICreepStatsSink
    {
        private readonly Dictionary<string, Dictionary<string, int>> _metrics = [];

        public void IncrementEnergyControl(string userId, int amount) { }
        public void IncrementEnergyCreeps(string userId, int amount) { }
        public void IncrementCreepsLost(string userId, int bodyParts) { }
        public void IncrementCreepsProduced(string userId, int bodyParts) { }
        public void IncrementSpawnRenewals(string userId) { }
        public void IncrementSpawnRecycles(string userId) { }
        public void IncrementSpawnCreates(string userId) { }
        public void IncrementTombstonesCreated(string userId) { }
        public void IncrementEnergyConstruction(string userId, int amount) { }
        public void IncrementEnergyHarvested(string userId, int amount) { }
        public Task FlushAsync(int gameTime, CancellationToken token = default) => Task.CompletedTask;
    }

    #endregion
}
