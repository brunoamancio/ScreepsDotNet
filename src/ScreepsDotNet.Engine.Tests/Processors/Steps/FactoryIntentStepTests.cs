namespace ScreepsDotNet.Engine.Tests.Processors.Steps;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.Bulk;
using ScreepsDotNet.Engine.Data.Models;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.Helpers;
using ScreepsDotNet.Engine.Processors.Steps;
using ScreepsDotNet.Engine.Tests.Processors.Helpers;

public sealed class FactoryIntentStepTests
{
    private readonly FactoryIntentStep _step = new();

    [Fact]
    public async Task BasicProduction_ProducesBattery()
    {
        // Arrange
        var factory = CreateFactory("f1", 10, 10, "user1", level: 0, energy: 600);
        var context = CreateContext([factory],
            CreateProduceIntent("user1", factory.Id, ResourceTypes.Battery), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == factory.Id);
        Assert.Equal(50, Payload.Store![ResourceTypes.Battery]);
        Assert.Equal(0, Payload.Store!.GetValueOrDefault(ResourceTypes.Energy, 0));
        Assert.Equal(110, Payload.Cooldown);
    }

    [Fact]
    public async Task ComplexProduction_ProducesUtriumBar()
    {
        // Arrange
        var factory = CreateFactory("f1", 10, 10, "user1", level: 0,
            utrium: 500, energy: 200);
        var context = CreateContext([factory],
            CreateProduceIntent("user1", factory.Id, ResourceTypes.UtriumBar), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == factory.Id);
        Assert.Equal(100, Payload.Store![ResourceTypes.UtriumBar]);
        Assert.Equal(0, Payload.Store!.GetValueOrDefault(ResourceTypes.Utrium, 0));
        Assert.Equal(0, Payload.Store!.GetValueOrDefault(ResourceTypes.Energy, 0));
        Assert.Equal(120, Payload.Cooldown);
    }

    [Fact]
    public async Task InsufficientComponents_DoesNothing()
    {
        // Arrange
        var factory = CreateFactory("f1", 10, 10, "user1", level: 0, energy: 100);
        var context = CreateContext([factory],
            CreateProduceIntent("user1", factory.Id, ResourceTypes.Battery), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task FactoryOnCooldown_DoesNothing()
    {
        // Arrange
        var factory = CreateFactory("f1", 10, 10, "user1", level: 0, energy: 600, cooldown: 200);
        var context = CreateContext([factory],
            CreateProduceIntent("user1", factory.Id, ResourceTypes.Battery), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task InsufficientCapacity_DoesNothing()
    {
        // Arrange - Factory has resources but store is full
        var factory = CreateFactory("f1", 10, 10, "user1", level: 0,
            energy: 600, battery: ScreepsGameConstants.FactoryCapacity);
        var context = CreateContext([factory],
            CreateProduceIntent("user1", factory.Id, ResourceTypes.Battery), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task UnknownCommodity_DoesNothing()
    {
        // Arrange
        var factory = CreateFactory("f1", 10, 10, "user1", level: 0, energy: 600);
        var context = CreateContext([factory],
            CreateProduceIntent("user1", factory.Id, "invalid_commodity"), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task MultipleProductions_AccumulatesCorrectly()
    {
        // Arrange
        var factory = CreateFactory("f1", 10, 10, "user1", level: 0, energy: 1800);
        var context = CreateContext([factory],
            CreateMultipleProduceIntents("user1", factory.Id, ResourceTypes.Battery, count: 3), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == factory.Id);
        Assert.Equal(150, Payload.Store![ResourceTypes.Battery]);
        Assert.Equal(0, Payload.Store!.GetValueOrDefault(ResourceTypes.Energy, 0));
        Assert.Equal(130, Payload.Cooldown);
    }

    [Fact]
    public async Task ActionLog_RecordsProduction()
    {
        // Arrange
        var factory = CreateFactory("f1", 10, 10, "user1", level: 0, energy: 600);
        var context = CreateContext([factory],
            CreateProduceIntent("user1", factory.Id, ResourceTypes.Battery), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == factory.Id);
        Assert.NotNull(Payload.ActionLog);
        Assert.NotNull(Payload.ActionLog!.Produce);
        Assert.Equal(ResourceTypes.Battery, Payload.ActionLog!.Produce!.ResourceType);
    }

    [Fact]
    public async Task WithNoController_DoesNotProcess()
    {
        // Arrange
        var factory = CreateFactory("f1", 10, 10, "user1", level: 0, energy: 600);
        var context = CreateContext([factory],
            CreateProduceIntent("user1", factory.Id, ResourceTypes.Battery), gameTime: 100, includeController: false);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - No patches emitted (factory not active without controller)
        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task WithOperateFactoryEffectLevel1_AllowsLevel1Commodity()
    {
        // Arrange - Level 0 factory with effect level 1 can produce level 1 commodity (Switch)
        var factory = CreateFactoryWithCustomStore("f1", 10, 10, "user1", level: 0,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [ResourceTypes.Wire] = 40,
                [ResourceTypes.Oxidant] = 95,
                [ResourceTypes.UtriumBar] = 35,
                [ResourceTypes.Energy] = 20
            },
            PowerTypes.OperateFactory, effectLevel: 1, endTime: 200);
        var context = CreateContext([factory],
            CreateProduceIntent("user1", factory.Id, ResourceTypes.Switch), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - Effect level 1 adds +1 to factory level (0 + 1 = 1), allows level 1 commodity
        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == factory.Id);
        Assert.Equal(5, Payload.Store![ResourceTypes.Switch]);
        Assert.Equal(0, Payload.Store!.GetValueOrDefault(ResourceTypes.Wire, 0));
        Assert.Equal(0, Payload.Store!.GetValueOrDefault(ResourceTypes.Oxidant, 0));
        Assert.Equal(0, Payload.Store!.GetValueOrDefault(ResourceTypes.UtriumBar, 0));
        Assert.Equal(0, Payload.Store!.GetValueOrDefault(ResourceTypes.Energy, 0));
    }

    [Fact]
    public async Task WithOperateFactoryEffectLevel3_AllowsLevel3Commodity()
    {
        // Arrange - Level 0 factory with effect level 3 can produce level 3 commodity (Liquid)
        var factory = CreateFactoryWithCustomStore("f1", 10, 10, "user1", level: 0,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [ResourceTypes.Oxidant] = 12,
                [ResourceTypes.Reductant] = 12,
                [ResourceTypes.GhodiumMelt] = 12,
                [ResourceTypes.Energy] = 90
            },
            PowerTypes.OperateFactory, effectLevel: 3, endTime: 200);
        var context = CreateContext([factory],
            CreateProduceIntent("user1", factory.Id, ResourceTypes.Liquid), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - Effect level 3 adds +3 to factory level (0 + 3 = 3), allows level 3 commodity
        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == factory.Id);
        Assert.Equal(12, Payload.Store![ResourceTypes.Liquid]);
        Assert.Equal(0, Payload.Store!.GetValueOrDefault(ResourceTypes.Oxidant, 0));
        Assert.Equal(0, Payload.Store!.GetValueOrDefault(ResourceTypes.Reductant, 0));
        Assert.Equal(0, Payload.Store!.GetValueOrDefault(ResourceTypes.GhodiumMelt, 0));
        Assert.Equal(0, Payload.Store!.GetValueOrDefault(ResourceTypes.Energy, 0));
    }

    [Fact]
    public async Task WithOperateFactoryEffectLevel5_AllowsLevel5Commodity()
    {
        // Arrange - Level 0 factory with effect level 5 can produce level 5 commodity (Device)
        var factory = CreateFactoryWithCustomStore("f1", 10, 10, "user1", level: 0,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [ResourceTypes.Circuit] = 1,
                [ResourceTypes.Microchip] = 3,
                [ResourceTypes.Crystal] = 110,
                [ResourceTypes.GhodiumMelt] = 150,
                [ResourceTypes.Energy] = 64
            },
            PowerTypes.OperateFactory, effectLevel: 5, endTime: 200);
        var context = CreateContext([factory],
            CreateProduceIntent("user1", factory.Id, ResourceTypes.Device), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - Effect level 5 adds +5 to factory level (0 + 5 = 5), allows level 5 commodity
        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == factory.Id);
        Assert.Equal(1, Payload.Store![ResourceTypes.Device]);
        Assert.Equal(0, Payload.Store!.GetValueOrDefault(ResourceTypes.Circuit, 0));
        Assert.Equal(0, Payload.Store!.GetValueOrDefault(ResourceTypes.Microchip, 0));
        Assert.Equal(0, Payload.Store!.GetValueOrDefault(ResourceTypes.Crystal, 0));
        Assert.Equal(0, Payload.Store!.GetValueOrDefault(ResourceTypes.GhodiumMelt, 0));
        Assert.Equal(0, Payload.Store!.GetValueOrDefault(ResourceTypes.Energy, 0));
    }

    #region Helper Methods

    private static RoomObjectSnapshot CreateFactory(string id, int x, int y, string userId, int level = 0,
        int energy = 0, int battery = 0, int utrium = 0, int cooldown = 0)
    {
        var store = new Dictionary<string, int>(StringComparer.Ordinal);
        if (energy > 0) store[ResourceTypes.Energy] = energy;
        if (battery > 0) store[ResourceTypes.Battery] = battery;
        if (utrium > 0) store[ResourceTypes.Utrium] = utrium;

        var currentCooldown = cooldown > 0 ? cooldown : (int?)null;

        var result = new RoomObjectSnapshot(
            id,
            RoomObjectTypes.Factory,
            "W1N1",
            "shard0",
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
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [ResourceTypes.Energy] = ScreepsGameConstants.FactoryCapacity
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
            Cooldown: currentCooldown,
            CooldownTime: null,
            SafeMode: null,
            SafeModeAvailable: null,
            PortalDestination: null,
            Send: null);
        return result;
    }

    private static RoomObjectSnapshot CreateFactoryWithEffect(string id, int x, int y, string userId, int level, int battery, int energy, PowerTypes powerType, int effectLevel, int endTime, int cooldown = 0)
    {
        var store = new Dictionary<string, int>(StringComparer.Ordinal);
        if (energy > 0) store[ResourceTypes.Energy] = energy;
        if (battery > 0) store[ResourceTypes.Battery] = battery;

        var currentCooldown = cooldown > 0 ? cooldown : (int?)null;

        var result = new RoomObjectSnapshot(
            id,
            RoomObjectTypes.Factory,
            "W1N1",
            "shard0",
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
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [ResourceTypes.Energy] = ScreepsGameConstants.FactoryCapacity
            },
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>()
            {
                [powerType] = new(powerType, effectLevel, endTime)
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
            Cooldown: currentCooldown,
            CooldownTime: null,
            SafeMode: null,
            SafeModeAvailable: null,
            PortalDestination: null,
            Send: null);
        return result;
    }

    private static RoomObjectSnapshot CreateFactoryWithCustomStore(string id, int x, int y, string userId, int level, Dictionary<string, int> store, PowerTypes powerType, int effectLevel, int endTime, int cooldown = 0)
    {
        var currentCooldown = cooldown > 0 ? cooldown : (int?)null;

        var result = new RoomObjectSnapshot(
            id,
            RoomObjectTypes.Factory,
            "W1N1",
            "shard0",
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
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [ResourceTypes.Energy] = ScreepsGameConstants.FactoryCapacity
            },
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>()
            {
                [powerType] = new(powerType, effectLevel, endTime)
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
            Cooldown: currentCooldown,
            CooldownTime: null,
            SafeMode: null,
            SafeModeAvailable: null,
            PortalDestination: null,
            Send: null);
        return result;
    }

    private static RoomIntentSnapshot CreateProduceIntent(string userId, string factoryId, string commodityType)
    {
        var fields = new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.ResourceType] = new(IntentFieldValueKind.Text, TextValue: commodityType)
        };
        var argument = new IntentArgument(fields);
        var record = new IntentRecord(IntentKeys.Produce, [argument]);
        var objectIntents = new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal)
        {
            [factoryId] = [record]
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

    private static RoomIntentSnapshot CreateMultipleProduceIntents(string userId, string factoryId, string commodityType, int count)
    {
        var fields = new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.ResourceType] = new(IntentFieldValueKind.Text, TextValue: commodityType)
        };
        var argument = new IntentArgument(fields);
        var records = Enumerable.Range(0, count)
            .Select(_ => new IntentRecord(IntentKeys.Produce, [argument]))
            .ToList();

        var objectIntents = new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal)
        {
            [factoryId] = records
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

    private static RoomProcessorContext CreateContext(IEnumerable<RoomObjectSnapshot> objects, RoomIntentSnapshot? intents = null, int gameTime = 100, bool includeController = true)
    {
        var objectMap = objects.ToDictionary(o => o.Id, o => o, StringComparer.Ordinal);

        // Add controller for structure activation validation (RCL 8 by default)
        if (includeController) {
            var controller = CreateController("controller1", 30, 30, "user1", level: 8);
            objectMap[controller.Id] = controller;
        }

        var state = new RoomState(
            "W1N1",
            gameTime,
            null,
            objectMap,
            new Dictionary<string, UserState>(StringComparer.Ordinal),
            intents,
            new Dictionary<string, RoomTerrainSnapshot>(StringComparer.Ordinal),
            []);

        var result = new RoomProcessorContext(state, new FakeMutationWriter(), new FakeCreepStatsSink(), new NullGlobalMutationWriter());
        return result;
    }

    private static RoomObjectSnapshot CreateController(string id, int x, int y, string userId, int level)
    {
        var result = new RoomObjectSnapshot(
            id,
            RoomObjectTypes.Controller,
            "W1N1",
            "shard0",
            userId,
            x,
            y,
            Hits: 0,
            HitsMax: 0,
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
            Progress: 0,
            ProgressTotal: 100000,
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
        return result;
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

#pragma warning disable CA1822 // Mark members as static
        public int GetMutationCount() => 0;
#pragma warning restore CA1822

        public Task FlushAsync(CancellationToken token = default) => Task.CompletedTask;

#pragma warning disable CA1822 // Method cannot be static as it implements interface member
        public bool TryGetPendingPatch(string objectId, out RoomObjectPatchPayload patch) { patch = new RoomObjectPatchPayload(); return false; }

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
#pragma warning disable CA1822 // Mark members as static
        public int GetMutationCount() => 0;
#pragma warning restore CA1822

        public Task FlushAsync(int gameTime, CancellationToken token = default) => Task.CompletedTask;
    }

    #endregion
}
