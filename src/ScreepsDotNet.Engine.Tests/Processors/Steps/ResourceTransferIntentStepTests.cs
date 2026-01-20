namespace ScreepsDotNet.Engine.Tests.Processors.Steps;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.Bulk;
using ScreepsDotNet.Engine.Data.Models;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.Helpers;
using ScreepsDotNet.Engine.Processors.Steps;

public sealed class ResourceTransferIntentStepTests
{
    private readonly ResourceTransferIntentStep _step = new(new ResourceDropHelper());

    [Fact]
    public async Task Transfer_BasicEnergyTransfer_UpdatesBothStores()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1", 100, store: new Dictionary<string, int> { [ResourceTypes.Energy] = 50 });
        var terminal = CreateTerminal("terminal1", 11, 10, "user1", store: [], capacity: 300_000);
        var context = CreateContext([creep, terminal], CreateTransferIntent("user1", creep.Id, terminal.Id, ResourceTypes.Energy, 30));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (_, payload) = writer.Patches.Single(p => p.ObjectId == creep.Id && p.Payload.Store is not null);
        Assert.Equal(20, payload.Store![ResourceTypes.Energy]);

        var terminalPatch = writer.Patches.Single(p => p.ObjectId == terminal.Id && p.Payload.Store is not null);
        Assert.Equal(30, terminalPatch.Payload.Store![ResourceTypes.Energy]);
    }

    [Fact]
    public async Task Transfer_ExceedsAvailable_ClampsToAvailable()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1", 100, store: new Dictionary<string, int> { [ResourceTypes.Energy] = 10 });
        var terminal = CreateTerminal("terminal1", 11, 10, "user1", store: [], capacity: 300_000);
        var context = CreateContext([creep, terminal], CreateTransferIntent("user1", creep.Id, terminal.Id, ResourceTypes.Energy, 50));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (_, payload) = writer.Patches.Single(p => p.ObjectId == creep.Id && p.Payload.Store is not null);
        Assert.Equal(0, payload.Store![ResourceTypes.Energy]);

        var terminalPatch = writer.Patches.Single(p => p.ObjectId == terminal.Id && p.Payload.Store is not null);
        Assert.Equal(10, terminalPatch.Payload.Store![ResourceTypes.Energy]);
    }



    [Fact]
    public async Task Withdraw_BasicWithdraw_UpdatesBothStores()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1", 100, store: []);
        var terminal = CreateTerminal("terminal1", 11, 10, "user1", store: new Dictionary<string, int> { [ResourceTypes.Energy] = 100 }, capacity: 300_000);
        var context = CreateContext([creep, terminal], CreateWithdrawIntent("user1", creep.Id, terminal.Id, ResourceTypes.Energy, 30));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (_, payload) = writer.Patches.Single(p => p.ObjectId == creep.Id && p.Payload.Store is not null);
        Assert.Equal(30, payload.Store![ResourceTypes.Energy]);

        var terminalPatch = writer.Patches.Single(p => p.ObjectId == terminal.Id && p.Payload.Store is not null);
        Assert.Equal(70, terminalPatch.Payload.Store![ResourceTypes.Energy]);
    }

    [Fact]
    public async Task Withdraw_SafeModeActive_BlocksWithdraw()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1", 100, store: []);
        var terminal = CreateTerminal("terminal1", 11, 10, "user2", store: new Dictionary<string, int> { [ResourceTypes.Energy] = 100 }, capacity: 300_000);
        var controller = CreateController("controller1", 25, 25, "user2", safeMode: 100);
        var context = CreateContext(
            [creep, terminal, controller],
            CreateWithdrawIntent("user1", creep.Id, terminal.Id, ResourceTypes.Energy, 30));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task Withdraw_PrivateRampart_BlocksWithdraw()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1", 100, store: []);
        var terminal = CreateTerminal("terminal1", 11, 10, "user2", store: new Dictionary<string, int> { [ResourceTypes.Energy] = 100 }, capacity: 300_000);
        var rampart = CreateRampart("rampart1", 11, 10, "user2", isPublic: false);
        var context = CreateContext([creep, terminal, rampart], CreateWithdrawIntent("user1", creep.Id, terminal.Id, ResourceTypes.Energy, 30));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task Withdraw_PublicRampart_AllowsWithdraw()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1", 100, store: []);
        var terminal = CreateTerminal("terminal1", 11, 10, "user2", store: new Dictionary<string, int> { [ResourceTypes.Energy] = 100 }, capacity: 300_000);
        var rampart = CreateRampart("rampart1", 11, 10, "user2", isPublic: true);
        var context = CreateContext([creep, terminal, rampart], CreateWithdrawIntent("user1", creep.Id, terminal.Id, ResourceTypes.Energy, 30));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (_, payload) = writer.Patches.Single(p => p.ObjectId == creep.Id && p.Payload.Store is not null);
        Assert.Equal(30, payload.Store![ResourceTypes.Energy]);
    }



    [Fact]
    public async Task Pickup_BasicPickup_RemovesDrop()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1", 100, store: []);
        var drop = CreateDrop("drop1", 11, 10, ResourceTypes.Energy, 50);
        var context = CreateContext([creep, drop], CreatePickupIntent("user1", creep.Id, drop.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (_, payload) = writer.Patches.Single(p => p.ObjectId == creep.Id && p.Payload.Store is not null);
        Assert.Equal(50, payload.Store![ResourceTypes.Energy]);

        var removal = writer.Removals.Single();
        Assert.Equal(drop.Id, removal);
    }

    [Fact]
    public async Task Pickup_PartialPickup_UpdatesDrop()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1", 30, store: []);
        var drop = CreateDrop("drop1", 11, 10, ResourceTypes.Energy, 50);
        var context = CreateContext([creep, drop], CreatePickupIntent("user1", creep.Id, drop.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (_, payload) = writer.Patches.Single(p => p.ObjectId == creep.Id && p.Payload.Store is not null);
        Assert.Equal(30, payload.Store![ResourceTypes.Energy]);

        var updatedDrop = writer.Upserts.Single();
        Assert.Equal(20, updatedDrop.ResourceAmount);
    }

    [Fact]
    public async Task Drop_BasicDrop_CreatesGroundDrop()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1", 100, store: new Dictionary<string, int> { [ResourceTypes.Energy] = 50 });
        var context = CreateContext([creep], CreateDropIntent("user1", creep.Id, ResourceTypes.Energy, 30));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (_, payload) = writer.Patches.Single(p => p.ObjectId == creep.Id && p.Payload.Store is not null);
        Assert.Equal(20, payload.Store![ResourceTypes.Energy]);

        var drop = writer.Upserts.Single();
        Assert.Equal(RoomObjectTypes.Resource, drop.Type);
        Assert.Equal(ResourceTypes.Energy, drop.ResourceType);
        Assert.Equal(30, drop.ResourceAmount);
        Assert.Equal(creep.X, drop.X);
        Assert.Equal(creep.Y, drop.Y);
    }

    [Fact]
    public async Task Drop_ContainerAtPosition_TransfersToContainer()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1", 100, store: new Dictionary<string, int> { [ResourceTypes.Energy] = 50 });
        var container = CreateContainer("container1", 10, 10, store: [], capacity: 2000);
        var context = CreateContext([creep, container], CreateDropIntent("user1", creep.Id, ResourceTypes.Energy, 30));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (_, payload) = writer.Patches.Single(p => p.ObjectId == creep.Id && p.Payload.Store is not null);
        Assert.Equal(20, payload.Store![ResourceTypes.Energy]);

        var containerPatch = writer.Patches.Single(p => p.ObjectId == container.Id && p.Payload.Store is not null);
        Assert.Equal(30, containerPatch.Payload.Store![ResourceTypes.Energy]);

        Assert.Empty(writer.Upserts);
    }

    private static RoomProcessorContext CreateContext(
        IEnumerable<RoomObjectSnapshot> objects,
        RoomIntentSnapshot intents,
        int gameTime = 100,
        int? safeMode = null,
        string? safeModeOwner = null)
    {
        var objectMap = objects.ToDictionary(o => o.Id, o => o, StringComparer.Ordinal);
        RoomInfoSnapshot? info = null;

        var state = new RoomState(
            "W1N1",
            gameTime,
            info,
            objectMap,
            new Dictionary<string, UserState>(StringComparer.Ordinal),
            intents,
            new Dictionary<string, RoomTerrainSnapshot>(StringComparer.Ordinal),
            []);

        return new RoomProcessorContext(state, new FakeMutationWriter(), new NullCreepStatsSink());
    }

    private static RoomIntentSnapshot CreateTransferIntent(string userId, string creepId, string targetId, string resourceType, int amount)
    {
        var argument = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: targetId),
            [IntentKeys.ResourceType] = new(IntentFieldValueKind.Text, TextValue: resourceType),
            [IntentKeys.Amount] = new(IntentFieldValueKind.Number, NumberValue: amount)
        });

        return CreateIntent(userId, creepId, IntentKeys.Transfer, argument);
    }

    private static RoomIntentSnapshot CreateWithdrawIntent(string userId, string creepId, string targetId, string resourceType, int amount)
    {
        var argument = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: targetId),
            [IntentKeys.ResourceType] = new(IntentFieldValueKind.Text, TextValue: resourceType),
            [IntentKeys.Amount] = new(IntentFieldValueKind.Number, NumberValue: amount)
        });

        return CreateIntent(userId, creepId, IntentKeys.Withdraw, argument);
    }

    private static RoomIntentSnapshot CreatePickupIntent(string userId, string creepId, string targetId)
    {
        var argument = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: targetId)
        });

        return CreateIntent(userId, creepId, IntentKeys.Pickup, argument);
    }

    private static RoomIntentSnapshot CreateDropIntent(string userId, string creepId, string resourceType, int amount)
    {
        var argument = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.ResourceType] = new(IntentFieldValueKind.Text, TextValue: resourceType),
            [IntentKeys.Amount] = new(IntentFieldValueKind.Number, NumberValue: amount)
        });

        return CreateIntent(userId, creepId, IntentKeys.Drop, argument);
    }

    private static RoomIntentSnapshot CreateIntent(string userId, string creepId, string intentName, IntentArgument argument)
    {
        var record = new IntentRecord(intentName, [argument]);
        var objectIntents = new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal)
        {
            [creepId] = [record]
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

        return new RoomIntentSnapshot("W1N1", "shard0", users);
    }

    private static RoomObjectSnapshot CreateCreep(
        string id,
        int x,
        int y,
        string userId,
        int capacity,
        Dictionary<string, int>? store = null)
        => new(
            id,
            RoomObjectTypes.Creep,
            "W1N1",
            "shard0",
            userId,
            x,
            y,
            Hits: 100,
            HitsMax: 100,
            Fatigue: 0,
            TicksToLive: 1000,
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
            Body: [],
            IsSpawning: false,
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
            InvaderHarvested: null,
            MineralAmount: null,
            Harvested: null,
            Cooldown: null,
            CooldownTime: null);

    private static RoomObjectSnapshot CreateTerminal(string id, int x, int y, string userId, Dictionary<string, int> store, int capacity)
        => new(
            id,
            RoomObjectTypes.Terminal,
            "W1N1",
            "shard0",
            userId,
            x,
            y,
            Hits: 3000,
            HitsMax: 3000,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.Terminal,
            Store: store,
            StoreCapacity: capacity,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: []);

    private static RoomObjectSnapshot CreateLab(
        string id,
        int x,
        int y,
        string userId,
        Dictionary<string, int>? store = null,
        Dictionary<string, int>? storeCapacityResource = null)
        => new(
            id,
            RoomObjectTypes.Lab,
            "W1N1",
            "shard0",
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
            Store: store ?? new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: ScreepsGameConstants.LabEnergyCapacity,
            StoreCapacityResource: storeCapacityResource ?? new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: []);

    private static RoomObjectSnapshot CreateRampart(string id, int x, int y, string userId, bool isPublic)
        => new(
            id,
            RoomObjectTypes.Rampart,
            "W1N1",
            "shard0",
            userId,
            x,
            y,
            Hits: 1000,
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
            Spawning: null,
            Body: [],
            IsSpawning: null,
            UserSummoned: null,
            IsPublic: isPublic,
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
            InvaderHarvested: null,
            MineralAmount: null,
            Harvested: null,
            Cooldown: null,
            CooldownTime: null);

    private static RoomObjectSnapshot CreateDrop(string id, int x, int y, string resourceType, int amount)
        => new(
            id,
            RoomObjectTypes.Resource,
            "W1N1",
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
            ResourceType: resourceType,
            ResourceAmount: amount,
            Progress: null,
            ProgressTotal: null,
            ActionLog: null,
            Energy: null,
            InvaderHarvested: null,
            MineralAmount: null,
            Harvested: null,
            Cooldown: null,
            CooldownTime: null);

    private static RoomObjectSnapshot CreateContainer(string id, int x, int y, Dictionary<string, int> store, int capacity)
        => new(
            id,
            RoomObjectTypes.Container,
            "W1N1",
            "shard0",
            null,
            x,
            y,
            Hits: 250_000,
            HitsMax: 250_000,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.Container,
            Store: store,
            StoreCapacity: capacity,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: []);

    private static RoomObjectSnapshot CreateController(string id, int x, int y, string userId, int? safeMode = null)
        => new(
            id,
            RoomObjectTypes.Controller,
            "W1N1",
            "shard0",
            userId,
            x,
            y,
            Hits: null,
            HitsMax: null,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: 1,
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
            Progress: null,
            ProgressTotal: null,
            ActionLog: null,
            Energy: null,
            InvaderHarvested: null,
            MineralAmount: null,
            Harvested: null,
            Cooldown: null,
            CooldownTime: null,
            SafeMode: safeMode);

    private sealed class FakeMutationWriter : IRoomMutationWriter
    {
        public List<(string ObjectId, RoomObjectPatchPayload Payload)> Patches { get; } = [];
        public List<RoomObjectSnapshot> Upserts { get; } = [];
        public List<string> Removals { get; } = [];

        public void Upsert(RoomObjectSnapshot document) => Upserts.Add(document);

        public void Patch(string objectId, RoomObjectPatchPayload patch) => Patches.Add((objectId, patch));

        public void Remove(string objectId) => Removals.Add(objectId);

        public void SetRoomInfoPatch(RoomInfoPatchPayload patch) { }

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
}
