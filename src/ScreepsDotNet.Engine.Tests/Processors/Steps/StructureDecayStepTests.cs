namespace ScreepsDotNet.Engine.Tests.Processors.Steps;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.GlobalMutations;
using ScreepsDotNet.Engine.Data.Models;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.Helpers;
using ScreepsDotNet.Engine.Processors.Steps;
using ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

public sealed class StructureDecayStepTests
{
    [Fact]
    public async Task ExecuteAsync_Rampart_Decays300HitsEvery100Ticks()
    {
        // Arrange - Rampart with no DecayTime (should decay immediately)
        var rampart = CreateRampart("rampart1", hits: 1000, decayTime: null);
        var context = CreateContext(gameTime: 100, rampart);
        var writer = (CapturingMutationWriter)context.MutationWriter;
        var step = new StructureDecayStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - Should lose 300 hits and set next decay time
        var (_, payload) = Assert.Single(writer.Patches, p => p.ObjectId == rampart.Id);
        Assert.Equal(700, payload.Hits); // 1000 - 300
        Assert.Equal(200, payload.DecayTime); // 100 + 100
    }

    [Fact]
    public async Task ExecuteAsync_Rampart_DoesNotDecayBeforeDecayTime()
    {
        // Arrange - Rampart with DecayTime = 200 (will decay at tick 199 due to -1 parity quirk)
        var rampart = CreateRampart("rampart1", hits: 1000, decayTime: 200);
        var context = CreateContext(gameTime: 100, rampart);
        var writer = (CapturingMutationWriter)context.MutationWriter;
        var step = new StructureDecayStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - Should NOT decay yet (100 < 199)
        Assert.All(writer.Patches, p => Assert.NotEqual(rampart.Id, p.ObjectId));
    }

    [Fact]
    public async Task ExecuteAsync_Rampart_DecaysOneTickEarly_ParityQuirk()
    {
        // Arrange - Rampart with DecayTime = 200
        // Node.js uses `gameTime >= nextDecayTime-1`, so decay happens at tick 199 (not 200)
        var rampart = CreateRampart("rampart1", hits: 1000, decayTime: 200);
        var context = CreateContext(gameTime: 199, rampart); // One tick BEFORE decayTime
        var writer = (CapturingMutationWriter)context.MutationWriter;
        var step = new StructureDecayStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - Should decay one tick early (parity with Node.js)
        var (_, payload) = Assert.Single(writer.Patches, p => p.ObjectId == rampart.Id);
        Assert.Equal(700, payload.Hits); // 1000 - 300
        Assert.Equal(299, payload.DecayTime); // 199 + 100
    }

    [Fact]
    public async Task ExecuteAsync_Rampart_DecaysWhenDecayTimeReached()
    {
        // Arrange - Rampart with DecayTime reached
        var rampart = CreateRampart("rampart1", hits: 1000, decayTime: 200);
        var context = CreateContext(gameTime: 200, rampart);
        var writer = (CapturingMutationWriter)context.MutationWriter;
        var step = new StructureDecayStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - Should decay
        var (_, payload) = Assert.Single(writer.Patches, p => p.ObjectId == rampart.Id);
        Assert.Equal(700, payload.Hits); // 1000 - 300
        Assert.Equal(300, payload.DecayTime); // 200 + 100
    }

    [Fact]
    public async Task ExecuteAsync_Rampart_RemovesWhenHitsReachZero()
    {
        // Arrange - Rampart with less hits than decay amount
        var rampart = CreateRampart("rampart1", hits: 200, decayTime: null);
        var context = CreateContext(gameTime: 100, rampart);
        var writer = (CapturingMutationWriter)context.MutationWriter;
        var step = new StructureDecayStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - Should set hits to 0 and remove
        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == rampart.Id && p.Payload.Hits.HasValue);
        Assert.Equal(0, Payload.Hits);
        Assert.Contains(rampart.Id, writer.Removals);
    }

    [Fact]
    public async Task ExecuteAsync_Road_Decays100HitsEvery1000Ticks()
    {
        // Arrange - Road with no DecayTime
        var road = CreateRoad("road1", hits: 5000, decayTime: null);
        var context = CreateContext(gameTime: 100, road);
        var writer = (CapturingMutationWriter)context.MutationWriter;
        var step = new StructureDecayStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - Should lose 100 hits and set next decay time
        var (_, payload) = Assert.Single(writer.Patches, p => p.ObjectId == road.Id);
        Assert.Equal(4900, payload.Hits); // 5000 - 100
        Assert.Equal(1100, payload.DecayTime); // 100 + 1000
    }

    [Fact]
    public async Task ExecuteAsync_Wall_Decays1HitPerTick()
    {
        // Arrange - Wall (no DecayTime property, decays every tick)
        var wall = CreateWall("wall1", hits: 100);
        var context = CreateContext(gameTime: 100, wall);
        var writer = (CapturingMutationWriter)context.MutationWriter;
        var step = new StructureDecayStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - Should lose 1 hit (no DecayTime)
        var (_, payload) = Assert.Single(writer.Patches, p => p.ObjectId == wall.Id);
        Assert.Equal(99, payload.Hits); // 100 - 1
        Assert.Null(payload.DecayTime); // Walls don't use DecayTime
    }

    [Fact]
    public async Task ExecuteAsync_MultipleStructures_DecaysCorrectly()
    {
        // Arrange - Mix of structures
        var rampart1 = CreateRampart("rampart1", hits: 1000, decayTime: null);
        var rampart2 = CreateRampart("rampart2", hits: 500, decayTime: 200); // Not time yet
        var road = CreateRoad("road1", hits: 5000, decayTime: null);
        var wall = CreateWall("wall1", hits: 100);

        var context = CreateContext(gameTime: 100, rampart1, rampart2, road, wall);
        var writer = (CapturingMutationWriter)context.MutationWriter;
        var step = new StructureDecayStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - Only rampart1, road, and wall should decay (rampart2's time not reached)
        Assert.Equal(3, writer.Patches.Count);

        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == rampart1.Id);
        Assert.Equal(700, Payload.Hits);

        var roadPatch = writer.Patches.Single(p => p.ObjectId == road.Id);
        Assert.Equal(4900, roadPatch.Payload.Hits);

        var wallPatch = writer.Patches.Single(p => p.ObjectId == wall.Id);
        Assert.Equal(99, wallPatch.Payload.Hits);

        // rampart2 should NOT be patched
        Assert.All(writer.Patches, p => Assert.NotEqual(rampart2.Id, p.ObjectId));
    }

    private static RoomObjectSnapshot CreateRampart(string id, int hits, int? decayTime)
        => new(
            Id: id,
            Type: RoomObjectTypes.Rampart,
            RoomName: "W1N1",
            Shard: null,
            UserId: "user1",
            X: 25,
            Y: 25,
            Hits: hits,
            HitsMax: 1000,
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
            Body: [],
            DecayTime: decayTime);

    private static RoomObjectSnapshot CreateRoad(string id, int hits, int? decayTime)
        => new(
            Id: id,
            Type: RoomObjectTypes.Road,
            RoomName: "W1N1",
            Shard: null,
            UserId: null,
            X: 25,
            Y: 25,
            Hits: hits,
            HitsMax: 5000,
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
            Body: [],
            DecayTime: decayTime);

    private static RoomObjectSnapshot CreateWall(string id, int hits)
        => new(
            Id: id,
            Type: RoomObjectTypes.Wall,
            RoomName: "W1N1",
            Shard: null,
            UserId: null,
            X: 25,
            Y: 25,
            Hits: hits,
            HitsMax: 300_000_000,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.Wall,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Body: [],
            DecayTime: null); // Walls don't use DecayTime

    private static RoomProcessorContext CreateContext(int gameTime, params RoomObjectSnapshot[] objects)
    {
        var objectDict = objects.ToDictionary(o => o.Id, StringComparer.Ordinal);

        var roomState = new RoomState(
            RoomName: "W1N1",
            GameTime: gameTime,
            Info: null,
            Objects: objectDict,
            Users: new Dictionary<string, UserState>(StringComparer.Ordinal),
            Intents: null,
            Terrain: new Dictionary<string, RoomTerrainSnapshot>(StringComparer.Ordinal),
            Flags: []
        );

        var writer = new CapturingMutationWriter();
        var globalWriter = new NoOpGlobalMutationWriter();
        var statsSink = new NoOpCreepStatsSink();

        var context = new RoomProcessorContext(roomState, writer, statsSink, globalWriter, null);
        return context;
    }

    private sealed class NoOpGlobalMutationWriter : IGlobalMutationWriter
    {
        public void PatchPowerCreep(string powerCreepId, PowerCreepMutationPatch patch) { }
        public void RemovePowerCreep(string powerCreepId) { }
        public void UpsertPowerCreep(PowerCreepSnapshot snapshot) { }
        public void UpsertMarketOrder(MarketOrderSnapshot snapshot, bool isInterShard) { }
        public void PatchMarketOrder(string orderId, MarketOrderPatch patch, bool isInterShard) { }
        public void RemoveMarketOrder(string orderId, bool isInterShard) { }
        public void AdjustUserMoney(string userId, double newBalance) { }
        public void InsertUserMoneyLog(UserMoneyLogEntry entry) { }
        public void UpsertRoomObject(RoomObjectSnapshot snapshot) { }
        public void PatchRoomObject(string objectId, GlobalRoomObjectPatch patch) { }
        public void RemoveRoomObject(string objectId) { }
        public void InsertTransaction(TransactionLogEntry entry) { }
        public void AdjustUserResource(string userId, string resourceType, int newBalance) { }
        public void InsertUserResourceLog(UserResourceLogEntry entry) { }
        public void IncrementUserGcl(string userId, int amount) { }
        public void IncrementUserPower(string userId, double amount) { }
        public void DecrementUserPower(string userId, double amount) { }
        public Task FlushAsync(CancellationToken token = default) => Task.CompletedTask;
#pragma warning disable CA1822 // Method cannot be static as it implements interface member
        public bool TryGetPendingPatch(string objectId, out RoomObjectPatchPayload patch) { patch = new RoomObjectPatchPayload(); return false; }

        public void Reset() { }
    }

    private sealed class NoOpCreepStatsSink : ICreepStatsSink
    {
        public void IncrementEnergyCreeps(string userId, int amount) { }
        public void IncrementCreepsLost(string userId, int bodyParts) { }
        public void IncrementCreepsProduced(string userId, int bodyParts) { }
        public void IncrementSpawnRenewals(string userId) { }
        public void IncrementSpawnRecycles(string userId) { }
        public void IncrementSpawnCreates(string userId) { }
        public void IncrementTombstonesCreated(string userId) { }
        public void IncrementEnergyConstruction(string userId, int amount) { }
        public void IncrementEnergyHarvested(string userId, int amount) { }
        public void IncrementEnergyControl(string userId, int amount) { }
        public Task FlushAsync(int gameTime, CancellationToken token = default) => Task.CompletedTask;
    }
}
