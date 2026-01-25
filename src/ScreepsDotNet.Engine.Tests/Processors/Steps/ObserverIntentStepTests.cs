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

public sealed class ObserverIntentStepTests
{
    private readonly ObserverIntentStep _step = new();

    [Fact]
    public async Task ObserveRoom_SetsObserveRoomProperty()
    {
        // Arrange
        var observer = CreateObserver("observer1", "W5N5", 25, 25, "user1");
        var controller = CreateController("controller1", "W5N5", 20, 20, "user1", level: 8);
        var context = CreateContext([observer, controller],
            CreateObserveRoomIntent("user1", observer.Id, "W10N10"), gameTime: 100);
        var globalWriter = (FakeGlobalMutationWriter)context.GlobalMutationWriter;
        var roomWriter = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var globalPatch = Assert.Single(globalWriter.RoomObjectPatches);
        Assert.Equal(observer.Id, globalPatch.ObjectId);
        Assert.Equal("W10N10", globalPatch.Patch.ObserveRoom);

        var roomPatch = Assert.Single(roomWriter.Patches);
        Assert.Equal(observer.Id, roomPatch.ObjectId);
        Assert.NotNull(roomPatch.Payload.ActionLog);
        Assert.NotNull(roomPatch.Payload.ActionLog!.ObserveRoom);
        Assert.Equal("W10N10", roomPatch.Payload.ActionLog!.ObserveRoom!.RoomName);
    }

    [Fact]
    public async Task ObserveRoom_NotOwner_NoMutation()
    {
        // Arrange
        var observer = CreateObserver("observer1", "W5N5", 25, 25, "user2");
        var controller = CreateController("controller1", "W5N5", 20, 20, "user1", level: 8);
        var context = CreateContext([observer, controller],
            CreateObserveRoomIntent("user1", observer.Id, "W10N10"), gameTime: 100);
        var globalWriter = (FakeGlobalMutationWriter)context.GlobalMutationWriter;
        var roomWriter = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(globalWriter.RoomObjectPatches);
        Assert.Empty(roomWriter.Patches);
    }

    [Fact]
    public async Task ObserveRoom_InvalidRoomName_NoMutation()
    {
        // Arrange
        var observer = CreateObserver("observer1", "W5N5", 25, 25, "user1");
        var controller = CreateController("controller1", "W5N5", 20, 20, "user1", level: 8);
        var context = CreateContext([observer, controller],
            CreateObserveRoomIntent("user1", observer.Id, "invalid"), gameTime: 100);
        var globalWriter = (FakeGlobalMutationWriter)context.GlobalMutationWriter;
        var roomWriter = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(globalWriter.RoomObjectPatches);
        Assert.Empty(roomWriter.Patches);
    }

    [Fact]
    public async Task ObserveRoom_OutOfRange_NoMutation()
    {
        // Arrange
        var observer = CreateObserver("observer1", "W5N5", 25, 25, "user1");
        var controller = CreateController("controller1", "W5N5", 20, 20, "user1", level: 8);
        var context = CreateContext([observer, controller],
            CreateObserveRoomIntent("user1", observer.Id, "W20N20"), gameTime: 100);  // 15 rooms away
        var globalWriter = (FakeGlobalMutationWriter)context.GlobalMutationWriter;
        var roomWriter = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(globalWriter.RoomObjectPatches);
        Assert.Empty(roomWriter.Patches);
    }

    [Fact]
    public async Task ObserveRoom_WithOperateObserverEffect_ExtendsRange()
    {
        // Arrange
        var observer = CreateObserver("observer1", "W5N5", 25, 25, "user1", hasOperateObserverEffect: true, effectEndsAt: 200);
        var controller = CreateController("controller1", "W5N5", 20, 20, "user1", level: 8);
        var context = CreateContext([observer, controller],
            CreateObserveRoomIntent("user1", observer.Id, "W20N20"), gameTime: 100);  // 15 rooms away, but effect is active
        var globalWriter = (FakeGlobalMutationWriter)context.GlobalMutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var globalPatch = Assert.Single(globalWriter.RoomObjectPatches);
        Assert.Equal(observer.Id, globalPatch.ObjectId);
        Assert.Equal("W20N20", globalPatch.Patch.ObserveRoom);
    }

    [Fact]
    public async Task ObserveRoom_OperateObserverEffectExpired_RangeNotExtended()
    {
        // Arrange
        var observer = CreateObserver("observer1", "W5N5", 25, 25, "user1", hasOperateObserverEffect: true, effectEndsAt: 50);
        var controller = CreateController("controller1", "W5N5", 20, 20, "user1", level: 8);
        var context = CreateContext([observer, controller],
            CreateObserveRoomIntent("user1", observer.Id, "W20N20"), gameTime: 100);  // Effect expired
        var globalWriter = (FakeGlobalMutationWriter)context.GlobalMutationWriter;
        var roomWriter = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(globalWriter.RoomObjectPatches);
        Assert.Empty(roomWriter.Patches);
    }

    [Fact]
    public async Task ObserveRoom_RclTooLow_NoMutation()
    {
        // Arrange
        var observer = CreateObserver("observer1", "W5N5", 25, 25, "user1");
        var controller = CreateController("controller1", "W5N5", 20, 20, "user1", level: 7);  // RCL 7 too low for observer
        var context = CreateContext([observer, controller],
            CreateObserveRoomIntent("user1", observer.Id, "W10N10"), gameTime: 100);
        var globalWriter = (FakeGlobalMutationWriter)context.GlobalMutationWriter;
        var roomWriter = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(globalWriter.RoomObjectPatches);
        Assert.Empty(roomWriter.Patches);
    }

    [Fact]
    public async Task ObserveRoom_ActionLog_RecordsTargetRoom()
    {
        // Arrange
        var observer = CreateObserver("observer1", "W5N5", 25, 25, "user1");
        var controller = CreateController("controller1", "W5N5", 20, 20, "user1", level: 8);
        var context = CreateContext([observer, controller],
            CreateObserveRoomIntent("user1", observer.Id, "W10N10"), gameTime: 100);
        var roomWriter = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var roomPatch = Assert.Single(roomWriter.Patches);
        Assert.NotNull(roomPatch.Payload.ActionLog);
        Assert.NotNull(roomPatch.Payload.ActionLog!.ObserveRoom);
        Assert.Equal("W10N10", roomPatch.Payload.ActionLog!.ObserveRoom!.RoomName);
    }

    #region Helper Methods

    private static RoomObjectSnapshot CreateObserver(string id, string roomName, int x, int y, string userId, bool hasOperateObserverEffect = false, int effectEndsAt = 0)
    {
        var effects = new Dictionary<PowerTypes, PowerEffectSnapshot>();
        if (hasOperateObserverEffect) {
            effects[PowerTypes.OperateObserver] = new PowerEffectSnapshot(
                Power: PowerTypes.OperateObserver,
                Level: 1,
                EndTime: effectEndsAt
            );
        }

        var result = new RoomObjectSnapshot(
            id,
            RoomObjectTypes.Observer,
            roomName,
            "shard0",
            userId,
            x,
            y,
            Hits: ScreepsGameConstants.ObserverHits,
            HitsMax: ScreepsGameConstants.ObserverHits,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.Observer,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: effects,
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
        return result;
    }

    private static RoomObjectSnapshot CreateController(string id, string roomName, int x, int y, string userId, int level)
        => new(
            id,
            RoomObjectTypes.Controller,
            roomName,
            "shard0",
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
            Progress: 0,
            ProgressTotal: 1000,
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

    private static RoomIntentSnapshot CreateObserveRoomIntent(string userId, string observerId, string targetRoomName)
    {
        var fields = new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.RoomName] = new(IntentFieldValueKind.Text, TextValue: targetRoomName)
        };

        var argument = new IntentArgument(fields);
        var record = new IntentRecord(IntentKeys.ObserveRoom, [argument]);

        var objectIntents = new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal)
        {
            [observerId] = [record]
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

        var result = new RoomIntentSnapshot("W5N5", "shard0", users);
        return result;
    }

    private static RoomProcessorContext CreateContext(IEnumerable<RoomObjectSnapshot> objects, RoomIntentSnapshot intents, int gameTime)
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
            RoomName: "W5N5",
            GameTime: gameTime,
            Info: null,
            Objects: objectMap,
            Users: users,
            Intents: intents,
            Terrain: new Dictionary<string, RoomTerrainSnapshot>(StringComparer.Ordinal),
            Flags: []);

        return new RoomProcessorContext(room, new FakeMutationWriter(), new FakeCreepStatsSink(), new FakeGlobalMutationWriter(), new NullNotificationSink());
    }

    #endregion

    #region Test Helpers

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

    #endregion
}
