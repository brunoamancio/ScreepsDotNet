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

public sealed class RuinDecayStepTests
{
    private readonly RuinDecayStep _step;
    private readonly FakeResourceDropHelper _dropHelper;

    public RuinDecayStepTests()
    {
        _dropHelper = new FakeResourceDropHelper();
        _step = new RuinDecayStep(_dropHelper);
    }

    [Fact]
    public async Task Ruin_DecayTimePassed_RemovesRuinAndDropsResources()
    {
        // Arrange
        var ruin = CreateRuin("ruin1", "W1N1", 10, 10, decayTime: 100, energy: 500, keanium: 200);
        var context = CreateContext([ruin], gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var removal = Assert.Single(writer.Removals);
        Assert.Equal("ruin1", removal);

        Assert.Equal(2, _dropHelper.DroppedResources.Count);
        Assert.Contains(_dropHelper.DroppedResources, d => d.ResourceType == ResourceTypes.Energy && d.Amount == 500);
        Assert.Contains(_dropHelper.DroppedResources, d => d.ResourceType == ResourceTypes.Keanium && d.Amount == 200);
    }

    [Fact]
    public async Task Ruin_DecayTimeNotReached_NoMutation()
    {
        // Arrange
        var ruin = CreateRuin("ruin1", "W1N1", 10, 10, decayTime: 200, energy: 500);
        var context = CreateContext([ruin], gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(writer.Removals);
        Assert.Empty(_dropHelper.DroppedResources);
    }

    [Fact]
    public async Task Ruin_DecayTimeExactlyAtThreshold_RemovesRuin()
    {
        // Arrange - Node.js checks: gameTime >= decayTime - 1
        var ruin = CreateRuin("ruin1", "W1N1", 10, 10, decayTime: 100, energy: 100);
        var context = CreateContext([ruin], gameTime: 99);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var removal = Assert.Single(writer.Removals);
        Assert.Equal("ruin1", removal);
    }

    [Fact]
    public async Task Ruin_NoDecayTime_RemovesImmediately()
    {
        // Arrange
        var ruin = CreateRuin("ruin1", "W1N1", 10, 10, decayTime: null, energy: 100);
        var context = CreateContext([ruin], gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var removal = Assert.Single(writer.Removals);
        Assert.Equal("ruin1", removal);
    }

    [Fact]
    public async Task Ruin_EmptyStore_RemovesWithoutDrops()
    {
        // Arrange
        var ruin = CreateRuin("ruin1", "W1N1", 10, 10, decayTime: 100);
        var context = CreateContext([ruin], gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var removal = Assert.Single(writer.Removals);
        Assert.Equal("ruin1", removal);
        Assert.Empty(_dropHelper.DroppedResources);
    }

    [Fact]
    public async Task MultipleRuins_OnlyDecayedOnesRemoved()
    {
        // Arrange
        var ruin1 = CreateRuin("ruin1", "W1N1", 10, 10, decayTime: 100, energy: 100);
        var ruin2 = CreateRuin("ruin2", "W1N1", 20, 20, decayTime: 200, energy: 200);
        var ruin3 = CreateRuin("ruin3", "W1N1", 30, 30, decayTime: 150, energy: 300);
        var context = CreateContext([ruin1, ruin2, ruin3], gameTime: 150);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, writer.Removals.Count);
        Assert.Contains("ruin1", writer.Removals);
        Assert.Contains("ruin3", writer.Removals);
        Assert.DoesNotContain("ruin2", writer.Removals);

        Assert.Equal(2, _dropHelper.DroppedResources.Count);
        Assert.Contains(_dropHelper.DroppedResources, d => d.Amount == 100);
        Assert.Contains(_dropHelper.DroppedResources, d => d.Amount == 300);
    }

    [Fact]
    public async Task NonRuinObjects_Ignored()
    {
        // Arrange
        var creep = CreateCreep("creep1", "W1N1", 10, 10);
        var tombstone = CreateTombstone("tombstone1", "W1N1", 20, 20, decayTime: 100);
        var context = CreateContext([creep, tombstone], gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(writer.Removals);
        Assert.Empty(_dropHelper.DroppedResources);
    }

    [Fact]
    public async Task Ruin_MultipleResourceTypes_DropsAll()
    {
        // Arrange
        var store = new Dictionary<string, int>
        {
            [ResourceTypes.Energy] = 100,
            [ResourceTypes.Lemergium] = 50,
            [ResourceTypes.Zynthium] = 75,
            [ResourceTypes.Catalyst] = 25
        };
        var ruin = CreateRuin("ruin1", "W1N1", 10, 10, decayTime: 100, store: store);
        var context = CreateContext([ruin], gameTime: 100);

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(4, _dropHelper.DroppedResources.Count);
        Assert.Contains(_dropHelper.DroppedResources, d => d.ResourceType == ResourceTypes.Energy && d.Amount == 100);
        Assert.Contains(_dropHelper.DroppedResources, d => d.ResourceType == ResourceTypes.Lemergium && d.Amount == 50);
        Assert.Contains(_dropHelper.DroppedResources, d => d.ResourceType == ResourceTypes.Zynthium && d.Amount == 75);
        Assert.Contains(_dropHelper.DroppedResources, d => d.ResourceType == ResourceTypes.Catalyst && d.Amount == 25);
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

    private static RoomObjectSnapshot CreateRuin(string id, string roomName, int x, int y, int? decayTime = null, int? energy = null, int? keanium = null, Dictionary<string, int>? store = null)
    {
        var actualStore = store ?? new Dictionary<string, int>();
        if (energy.HasValue && !actualStore.ContainsKey(ResourceTypes.Energy))
            actualStore[ResourceTypes.Energy] = energy.Value;
        if (keanium.HasValue && !actualStore.ContainsKey(ResourceTypes.Keanium))
            actualStore[ResourceTypes.Keanium] = keanium.Value;

        var result = new RoomObjectSnapshot(
            id,
            RoomObjectTypes.Ruin,
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
            Store: actualStore,
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
            DecayTime: decayTime,
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
        return result;
    }

    private static RoomObjectSnapshot CreateTombstone(string id, string roomName, int x, int y, int? decayTime = null)
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
            DecayTime: decayTime,
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

    private sealed class FakeResourceDropHelper : IResourceDropHelper
    {
        public List<(RoomObjectSnapshot Origin, string ResourceType, int Amount)> DroppedResources { get; } = [];

        public ResourceDropContext CreateContext() => new();

        public void DropOverflowResources(RoomProcessorContext context, RoomObjectSnapshot origin, Dictionary<string, int> mutableStore, int overflow, Dictionary<string, int> storePatch, ResourceDropContext dropContext)
        {
            // Not used in these tests
        }

        public void DropResource(RoomProcessorContext context, RoomObjectSnapshot origin, string resourceType, int amount, ResourceDropContext dropContext)
            => DroppedResources.Add((origin, resourceType, amount));
    }
}
