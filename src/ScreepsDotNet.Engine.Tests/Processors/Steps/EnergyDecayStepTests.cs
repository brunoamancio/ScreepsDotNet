namespace ScreepsDotNet.Engine.Tests.Processors.Steps;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.Bulk;
using ScreepsDotNet.Engine.Data.GlobalMutations;
using ScreepsDotNet.Engine.Data.Models;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.Helpers;
using ScreepsDotNet.Engine.Processors.Steps;

public sealed class EnergyDecayStepTests
{
    private readonly EnergyDecayStep _step = new();

    [Fact]
    public async Task Energy_DecaysCorrectly_UpdatesAmount()
    {
        // Arrange
        var energy = CreateEnergy("energy1", "W1N1", 10, 10, ResourceTypes.Energy, 1000);
        var context = CreateContext([energy], gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (objectId, payload) = Assert.Single(writer.Patches);
        Assert.Equal("energy1", objectId);
        Assert.Equal(999, payload.Energy);  // 1000 - ceil(1000/1000) = 1000 - 1 = 999
    }

    [Fact]
    public async Task Energy_SmallAmount_DecaysToZeroAndRemoves()
    {
        // Arrange
        var energy = CreateEnergy("energy1", "W1N1", 10, 10, ResourceTypes.Energy, 1);
        var context = CreateContext([energy], gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(writer.Patches);
        var removal = Assert.Single(writer.Removals);
        Assert.Equal("energy1", removal);
    }

    [Fact]
    public async Task Energy_LargeAmount_DecaysProportionally()
    {
        // Arrange
        var energy = CreateEnergy("energy1", "W1N1", 10, 10, ResourceTypes.Energy, 5000);
        var context = CreateContext([energy], gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (objectId, payload) = Assert.Single(writer.Patches);
        Assert.Equal("energy1", objectId);
        Assert.Equal(4995, payload.Energy);  // 5000 - ceil(5000/1000) = 5000 - 5 = 4995
    }

    [Fact]
    public async Task Energy_DecayResultsInZero_Removes()
    {
        // Arrange - ceil(500/1000) = 1, so 500 - 1 = 499 (not zero yet)
        // But if amount <= decay amount, it should remove
        var energy = CreateEnergy("energy1", "W1N1", 10, 10, ResourceTypes.Energy, 1);
        var context = CreateContext([energy], gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var removal = Assert.Single(writer.Removals);
        Assert.Equal("energy1", removal);
        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task Energy_ZeroAmount_RemovedImmediately()
    {
        // Arrange
        var energy = CreateEnergy("energy1", "W1N1", 10, 10, ResourceTypes.Energy, 0);
        var context = CreateContext([energy], gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var removal = Assert.Single(writer.Removals);
        Assert.Equal("energy1", removal);
        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task MultipleEnergyDrops_AllDecayIndependently()
    {
        // Arrange
        var energy1 = CreateEnergy("energy1", "W1N1", 10, 10, ResourceTypes.Energy, 1000);
        var energy2 = CreateEnergy("energy2", "W1N1", 20, 20, ResourceTypes.Energy, 500);
        var energy3 = CreateEnergy("energy3", "W1N1", 30, 30, ResourceTypes.Energy, 1);
        var context = CreateContext([energy1, energy2, energy3], gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, writer.Patches.Count);
        Assert.Contains(writer.Patches, p => p.ObjectId == "energy1" && p.Payload.Energy == 999);
        Assert.Contains(writer.Patches, p => p.ObjectId == "energy2" && p.Payload.Energy == 499);

        var removal = Assert.Single(writer.Removals);
        Assert.Equal("energy3", removal);
    }

    [Fact]
    public async Task NonEnergyObjects_Ignored()
    {
        // Arrange
        var creep = CreateCreep("creep1", "W1N1", 10, 10);
        var tombstone = CreateTombstone("tombstone1", "W1N1", 20, 20);
        var context = CreateContext([creep, tombstone], gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(writer.Patches);
        Assert.Empty(writer.Removals);
    }

    [Fact]
    public async Task Power_AlsoDecays()
    {
        // Arrange - Power is also a dropped resource type
        var power = CreateEnergy("power1", "W1N1", 10, 10, ResourceTypes.Power, 500);
        var context = CreateContext([power], gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (objectId, payload) = Assert.Single(writer.Patches);
        Assert.Equal("power1", objectId);
        Assert.Equal(499, payload.Energy);  // 500 - ceil(500/1000) = 500 - 1 = 499
    }

    [Fact]
    public async Task Mineral_AlsoDecays()
    {
        // Arrange - Any resource type decays
        var uranium = CreateEnergy("uranium1", "W1N1", 10, 10, ResourceTypes.Utrium, 2500);
        var context = CreateContext([uranium], gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (objectId, payload) = Assert.Single(writer.Patches);
        Assert.Equal("uranium1", objectId);
        Assert.Equal(2497, payload.Energy);  // 2500 - ceil(2500/1000) = 2500 - 3 = 2497
    }

    [Fact]
    public async Task Energy_ExactlyThreshold_Decays()
    {
        // Arrange - 1000 decays to 999
        var energy = CreateEnergy("energy1", "W1N1", 10, 10, ResourceTypes.Energy, 1000);
        var context = CreateContext([energy], gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (objectId, payload) = Assert.Single(writer.Patches);
        Assert.Equal("energy1", objectId);
        Assert.Equal(999, payload.Energy);
    }

    private static RoomProcessorContext CreateContext(List<RoomObjectSnapshot> objects, int gameTime)
    {
        var objectMap = objects.ToDictionary(o => o.Id, StringComparer.Ordinal);
        var users = new Dictionary<string, UserState>(StringComparer.Ordinal)
        {
            ["user1"] = new(Id: "user1",
                            Username: "user1",
                            Cpu: 100,
                            Power: 0,
                            Money: 0,
                            Active: true,
                            PowerExperimentationTime: 0,
                            Resources: new Dictionary<string, int>(StringComparer.Ordinal))
        };

        var room = new RoomState(
            RoomName: "W1N1",
            GameTime: gameTime,
            Info: null,
            Objects: objectMap,
            Users: users,
            Intents: null,
            Terrain: new Dictionary<string, RoomTerrainSnapshot>(StringComparer.Ordinal),
            Flags: []);

        var result = new RoomProcessorContext(room, new FakeMutationWriter(), new FakeCreepStatsSink(), new FakeGlobalMutationWriter(), new NullNotificationSink());
        return result;
    }

    private static RoomObjectSnapshot CreateEnergy(string id, string roomName, int x, int y, string resourceType, int amount)
        => new(
            id,
            RoomObjectTypes.Resource,
            roomName,
            "shard0",
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
            StructureType: null,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Body: [],
            Spawning: null,
            IsSpawning: null,
            UserSummoned: null,
            IsPublic: null,
            NotifyWhenAttacked: null,
            StrongholdId: null,
            DeathTime: null,
            DecayTime: null,
            CreepId: null,
            CreepName: null,
            CreepTicksToLive: null,
            CreepSaying: null,
            ResourceType: resourceType,
            ResourceAmount: null,
            Progress: null,
            ProgressTotal: null,
            ActionLog: null,
            Energy: amount,
            MineralAmount: null,
            InvaderHarvested: null,
            Harvested: null,
            Cooldown: null,
            CooldownTime: null,
            NextRegenerationTime: null,
            SafeMode: null,
            SafeModeAvailable: null,
            PortalDestination: null,
            Send: null,
            Powers: null,
            MemorySourceId: null,
            MemoryMove: null,
            ObserveRoom: null);

    private static RoomObjectSnapshot CreateCreep(string id, string roomName, int x, int y)
        => new(
            id,
            RoomObjectTypes.Creep,
            roomName,
            "shard0",
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
            StructureType: null,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Body: [],
            Spawning: null,
            IsSpawning: null,
            UserSummoned: null,
            IsPublic: null,
            NotifyWhenAttacked: null,
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
            NextRegenerationTime: null,
            SafeMode: null,
            SafeModeAvailable: null,
            PortalDestination: null,
            Send: null,
            Powers: null,
            MemorySourceId: null,
            MemoryMove: null,
            ObserveRoom: null);

    private static RoomObjectSnapshot CreateTombstone(string id, string roomName, int x, int y)
        => new(
            id,
            RoomObjectTypes.Tombstone,
            roomName,
            "shard0",
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
            StructureType: null,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Body: [],
            Spawning: null,
            IsSpawning: null,
            UserSummoned: null,
            IsPublic: null,
            NotifyWhenAttacked: null,
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
            NextRegenerationTime: null,
            SafeMode: null,
            SafeModeAvailable: null,
            PortalDestination: null,
            Send: null,
            Powers: null,
            MemorySourceId: null,
            MemoryMove: null,
            ObserveRoom: null);

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

        public bool TryGetPendingPatch(string objectId, out RoomObjectPatchPayload patch)
        {
            patch = default!;
            return false;
        }

        public bool IsMarkedForRemoval(string objectId) => false;

        public Task FlushAsync(CancellationToken token = default) => Task.CompletedTask;

        public void Reset()
        {
            Patches.Clear();
            Upserts.Clear();
            Removals.Clear();
        }
    }

    private sealed class FakeGlobalMutationWriter : IGlobalMutationWriter
    {
        public List<RoomObjectSnapshot> RoomObjectUpserts { get; } = [];
        public List<(string ObjectId, GlobalRoomObjectPatch Patch)> RoomObjectPatches { get; } = [];
        public List<string> RoomObjectRemovals { get; } = [];

        public void UpsertRoomObject(RoomObjectSnapshot snapshot) => RoomObjectUpserts.Add(snapshot);

        public void PatchRoomObject(string objectId, GlobalRoomObjectPatch patch) => RoomObjectPatches.Add((objectId, patch));

        public void RemoveRoomObject(string objectId) => RoomObjectRemovals.Add(objectId);

        public void PatchPowerCreep(string powerCreepId, PowerCreepMutationPatch patch) { }
        public void RemovePowerCreep(string powerCreepId) { }
        public void UpsertPowerCreep(PowerCreepSnapshot snapshot) { }
        public void UpsertMarketOrder(MarketOrderSnapshot snapshot, bool isInterShard) { }
        public void PatchMarketOrder(string orderId, MarketOrderPatch patch, bool isInterShard) { }
        public void RemoveMarketOrder(string orderId, bool isInterShard) { }
        public void AdjustUserMoney(string userId, double newBalance) { }
        public void InsertUserMoneyLog(UserMoneyLogEntry entry) { }
        public void InsertTransaction(TransactionLogEntry entry) { }
        public void AdjustUserResource(string userId, string resourceType, int newBalance) { }
        public void InsertUserResourceLog(UserResourceLogEntry entry) { }
        public void IncrementUserGcl(string userId, int amount) { }
        public void IncrementUserPower(string userId, double amount) { }
        public void DecrementUserPower(string userId, double amount) { }

        public Task FlushAsync(CancellationToken token = default) => Task.CompletedTask;

        public void Reset()
        {
            RoomObjectUpserts.Clear();
            RoomObjectPatches.Clear();
            RoomObjectRemovals.Clear();
        }
    }

    private sealed class FakeCreepStatsSink : ICreepStatsSink
    {
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
}
