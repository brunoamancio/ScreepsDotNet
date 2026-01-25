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

public sealed class CombatResolutionStepTests
{
    [Fact]
    public async Task ExecuteAsync_CreepWithNotifyWhenAttackedTakesDamage_SendsNotification()
    {
        var attacker = CreateCreep("attacker", hits: 100, userId: "user1", x: 1, y: 1);
        var defender = CreateCreep("defender", hits: 100, userId: "user2", x: 1, y: 2, notifyWhenAttacked: true);
        var intents = CreateIntents(defender.Id, 30, userId: "user1", attackerId: attacker.Id);
        var notificationSink = new FakeNotificationSink();
        var context = CreateContext([attacker, defender], intents, notificationSink: notificationSink);
        var step = new CombatResolutionStep(new RecordingDeathProcessor());

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var notifications = notificationSink.Notifications;
        var (UserId, ObjectId, RoomName) = Assert.Single(notifications);
        Assert.Equal("user2", UserId);
        Assert.Equal(defender.Id, ObjectId);
        Assert.Equal("W1N1", RoomName);
    }

    [Fact]
    public async Task ExecuteAsync_CreepWithoutNotifyWhenAttackedTakesDamage_NoNotification()
    {
        var attacker = CreateCreep("attacker", hits: 100, userId: "user1", x: 1, y: 1);
        var defender = CreateCreep("defender", hits: 100, userId: "user2", x: 1, y: 2, notifyWhenAttacked: false);
        var intents = CreateIntents(defender.Id, 30, userId: "user1", attackerId: attacker.Id);
        var notificationSink = new FakeNotificationSink();
        var context = CreateContext([attacker, defender], intents, notificationSink: notificationSink);
        var step = new CombatResolutionStep(new RecordingDeathProcessor());

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(notificationSink.Notifications);
    }

    [Fact]
    public async Task ExecuteAsync_CreepKilledWithNotifyWhenAttacked_SendsNotification()
    {
        var attacker = CreateCreep("attacker", hits: 100, userId: "user1", x: 1, y: 1);
        var defender = CreateCreep("defender", hits: 50, userId: "user2", x: 1, y: 2, notifyWhenAttacked: true);
        var intents = CreateIntents(defender.Id, 50, userId: "user1", attackerId: attacker.Id);
        var notificationSink = new FakeNotificationSink();
        var context = CreateContext([attacker, defender], intents, notificationSink: notificationSink);
        var deathProcessor = new RecordingDeathProcessor();
        var step = new CombatResolutionStep(deathProcessor);

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var notifications = notificationSink.Notifications;
        var (UserId, ObjectId, RoomName) = Assert.Single(notifications);
        Assert.Equal("user2", UserId);
        Assert.Equal(defender.Id, ObjectId);
        Assert.Single(deathProcessor.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_StructureWithNotifyWhenAttackedTakesDamage_SendsNotification()
    {
        var attacker = CreateCreep("attacker", hits: 100, userId: "user1", x: 1, y: 1);
        var tower = CreateStructure("tower1", hits: 3000, userId: "user2", structureType: RoomObjectTypes.Tower, notifyWhenAttacked: true);
        var intents = CreateIntents(tower.Id, 30, userId: "user1", attackerId: attacker.Id);
        var notificationSink = new FakeNotificationSink();
        var context = CreateContext([attacker, tower], intents, notificationSink: notificationSink);
        var step = new CombatResolutionStep(new RecordingDeathProcessor());

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var notifications = notificationSink.Notifications;
        var (UserId, ObjectId, RoomName) = Assert.Single(notifications);
        Assert.Equal("user2", UserId);
        Assert.Equal(tower.Id, ObjectId);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleAttacksOnSameObject_SendsSingleNotification()
    {
        var attacker1 = CreateCreep("attacker1", hits: 100, userId: "user1", x: 1, y: 1);
        var attacker2 = CreateCreep("attacker2", hits: 100, userId: "user1", x: 2, y: 2);
        var defender = CreateCreep("defender", hits: 200, userId: "user2", x: 1, y: 2, notifyWhenAttacked: true);

        var creepIntents = new Dictionary<string, CreepIntentEnvelope>(StringComparer.Ordinal)
        {
            [attacker1.Id] = new(null, new AttackIntent(defender.Id, 30), null, false, null, null, new Dictionary<string, object?>(StringComparer.Ordinal)),
            [attacker2.Id] = new(null, new AttackIntent(defender.Id, 30), null, false, null, null, new Dictionary<string, object?>(StringComparer.Ordinal))
        };

        var envelope = new IntentEnvelope("user1",
                                          new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal),
                                          new Dictionary<string, SpawnIntentEnvelope>(StringComparer.Ordinal),
                                          creepIntents);

        var intents = new RoomIntentSnapshot("W1N1",
                                             "shard0",
                                             new Dictionary<string, IntentEnvelope>(StringComparer.Ordinal) { ["user1"] = envelope });

        var notificationSink = new FakeNotificationSink();
        var context = CreateContext([attacker1, attacker2, defender], intents, notificationSink: notificationSink);
        var step = new CombatResolutionStep(new RecordingDeathProcessor());

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var notifications = notificationSink.Notifications;
        var (UserId, ObjectId, RoomName) = Assert.Single(notifications);
        Assert.Equal("user2", UserId);
        Assert.Equal(defender.Id, ObjectId);
    }

    [Fact]
    public async Task ExecuteAsync_NotifyWhenAttackedNullTakesDamage_NoNotification()
    {
        var attacker = CreateCreep("attacker", hits: 100, userId: "user1", x: 1, y: 1);
        var defender = CreateCreep("defender", hits: 100, userId: "user2", x: 1, y: 2, notifyWhenAttacked: null);
        var intents = CreateIntents(defender.Id, 30, userId: "user1", attackerId: attacker.Id);
        var notificationSink = new FakeNotificationSink();
        var context = CreateContext([attacker, defender], intents, notificationSink: notificationSink);
        var step = new CombatResolutionStep(new RecordingDeathProcessor());

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(notificationSink.Notifications);
    }

    [Fact]
    public async Task ExecuteAsync_KillsCreep_InvokesDeathProcessor()
    {
        var attacker = CreateCreep("attacker", hits: 100);
        var creep = CreateCreep("creep1", hits: 50);
        var intents = CreateIntents(creep.Id, 50);
        var context = CreateContext([attacker, creep], intents);
        var writer = (FakeMutationWriter)context.MutationWriter;
        var deathProcessor = new RecordingDeathProcessor();
        var step = new CombatResolutionStep(deathProcessor);

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Single(deathProcessor.Calls);
        Assert.Equal(creep.Id, deathProcessor.Calls[0].Creep.Id);
        Assert.True(deathProcessor.Calls[0].Options.ViolentDeath);
        Assert.Empty(writer.Removals);
    }

    [Fact]
    public async Task ExecuteAsync_KillsStructure_RemovesDirectly()
    {
        var attacker = CreateCreep("attacker", hits: 100);
        var structure = CreateStructure("wall1", hits: 10);
        var intents = CreateIntents(structure.Id, 10);
        var context = CreateContext([attacker, structure], intents);
        var writer = (FakeMutationWriter)context.MutationWriter;
        var deathProcessor = new RecordingDeathProcessor();
        var step = new CombatResolutionStep(deathProcessor);

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Contains(structure.Id, writer.Removals);
        Assert.Empty(deathProcessor.Calls);
    }

    private static RoomProcessorContext CreateContext(IEnumerable<RoomObjectSnapshot> objects,
                                                      RoomIntentSnapshot intents,
                                                      int gameTime = 100,
                                                      bool includeController = false,
                                                      INotificationSink? notificationSink = null)
    {
        var objectMap = new Dictionary<string, RoomObjectSnapshot>(StringComparer.Ordinal);
        foreach (var obj in objects)
            objectMap[obj.Id] = obj;

        // Add controller for structure activation validation (RCL 8 by default)
        if (includeController) {
            var controller = new RoomObjectSnapshot(
                "controller1",
                RoomObjectTypes.Controller,
                "W1N1",
                "shard0",
                "user1",
                25,
                25,
                Hits: null,
                HitsMax: null,
                Fatigue: null,
                TicksToLive: null,
                Name: null,
                Level: 8,
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
                StrongholdId: null,
                DeathTime: null,
                DecayTime: null,
                CreepId: null,
                CreepName: null,
                CreepTicksToLive: null,
                CreepSaying: null,
                ResourceType: null,
                ResourceAmount: null);

            objectMap[controller.Id] = controller;
        }

        var state = new RoomState("W1N1",
                                  gameTime,
                                  null,
                                  objectMap,
                                  new Dictionary<string, UserState>(StringComparer.Ordinal),
                                  intents,
                                  new Dictionary<string, RoomTerrainSnapshot>(StringComparer.Ordinal),
                                  []);

        var actualNotificationSink = notificationSink ?? new NullNotificationSink();
        return new RoomProcessorContext(state, new FakeMutationWriter(), new NullCreepStatsSink(), new NullGlobalMutationWriter(), actualNotificationSink);
    }

    private static RoomIntentSnapshot CreateIntents(string targetId, int damage, string userId = "user1", string attackerId = "attacker")
    {
        var creepIntents = new Dictionary<string, CreepIntentEnvelope>(StringComparer.Ordinal)
        {
            [attackerId] = new(null,
                               new AttackIntent(targetId, damage),
                               null,
                               false,
                               null,
                               null,
                               new Dictionary<string, object?>(StringComparer.Ordinal))
        };

        var envelope = new IntentEnvelope(userId,
                                          new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal),
                                          new Dictionary<string, SpawnIntentEnvelope>(StringComparer.Ordinal),
                                          creepIntents);

        return new RoomIntentSnapshot("W1N1",
                                      "shard0",
                                      new Dictionary<string, IntentEnvelope>(StringComparer.Ordinal) { [userId] = envelope });
    }

    private static RoomObjectSnapshot CreateCreep(string id, int hits, string userId = "user1", int x = 10, int y = 10, bool? notifyWhenAttacked = null)
        => new(id,
               RoomObjectTypes.Creep,
               "W1N1",
               "shard0",
               userId,
               x,
               y,
               Hits: hits,
               HitsMax: hits,
               Fatigue: 0,
               TicksToLive: 100,
               Name: id,
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
               NotifyWhenAttacked: notifyWhenAttacked);

    private static RoomObjectSnapshot CreateStructure(string id, int hits, string? userId = null, string structureType = RoomObjectTypes.Wall, bool? notifyWhenAttacked = null)
        => new(id,
               structureType,
               "W1N1",
               "shard0",
               userId,
               10,
               11,
               Hits: hits,
               HitsMax: hits,
               Fatigue: null,
               TicksToLive: null,
               Name: id,
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
               NotifyWhenAttacked: notifyWhenAttacked);

    private sealed class RecordingDeathProcessor : ICreepDeathProcessor
    {
        public List<(RoomObjectSnapshot Creep, CreepDeathOptions Options)> Calls { get; } = [];

        public void Process(RoomProcessorContext context, RoomObjectSnapshot creep, CreepDeathOptions options, IDictionary<string, int> energyLedger)
            => Calls.Add((creep, options));
    }

    private sealed class FakeNotificationSink : INotificationSink
    {
        public List<(string UserId, string ObjectId, string RoomName)> Notifications { get; } = [];

        public void SendAttackedNotification(string userId, string objectId, string roomName)
            => Notifications.Add((userId, objectId, roomName));

        public Task FlushAsync(CancellationToken token = default) => Task.CompletedTask;
    }

    private sealed class FakeMutationWriter : IRoomMutationWriter
    {
        public List<(string ObjectId, RoomObjectPatchPayload Payload)> Patches { get; } = [];
        public List<string> Removals { get; } = [];

        public void Upsert(RoomObjectSnapshot document) { }

        public void Patch(string objectId, RoomObjectPatchPayload patch)
            => Patches.Add((objectId, patch));

        public void Remove(string objectId)
        {
            if (!string.IsNullOrWhiteSpace(objectId))
                Removals.Add(objectId);
        }

        public void SetRoomInfoPatch(RoomInfoPatchPayload patch) { }

        public void SetEventLog(IRoomEventLogPayload? eventLog) { }

        public void SetMapView(IRoomMapViewPayload? mapView) { }

#pragma warning disable CA1822 // Mark members as static
        public int GetMutationCount() => 0;
#pragma warning restore CA1822

        public Task FlushAsync(CancellationToken token = default) => Task.CompletedTask;

#pragma warning disable CA1822 // Method cannot be static as it implements interface member
        public bool TryGetPendingPatch(string objectId, out RoomObjectPatchPayload patch) { patch = new RoomObjectPatchPayload(); return false; }

        public void Reset() { }
    }
}
