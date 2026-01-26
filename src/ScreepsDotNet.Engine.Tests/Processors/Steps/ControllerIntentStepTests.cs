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

public sealed class ControllerIntentStepTests
{
    private readonly ControllerIntentStep _step = new();

    #region upgradeController Tests

    [Fact]
    public async Task Upgrade_BasicUpgrade_UpdatesProgressAndEnergy()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1",
            body: [new CreepBodyPartSnapshot(BodyPartType.Work, 100, null)],
            energy: 50);
        var controller = CreateController("ctrl1", 11, 11, "user1",
            level: 1, progress: 100, downgradeTime: 50000);
        var context = CreateContext([creep, controller],
            CreateUpgradeIntent("user1", creep.Id, controller.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == creep.Id && p.Payload.Store is not null);
        Assert.Equal(49, Payload.Store![ResourceTypes.Energy]);

        var ctrlPatch = writer.Patches.Single(p => p.ObjectId == controller.Id && p.Payload.Progress.HasValue);
        Assert.Equal(101, ctrlPatch.Payload.Progress);

        var stats = (FakeCreepStatsSink)context.Stats;
        Assert.Equal(1, stats.GetMetric("user1", RoomStatsMetricNames.EnergyControl));
    }

    [Fact]
    public async Task Upgrade_MultipleInSameTick_AccumulatesInLedger()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1",
            body: [new CreepBodyPartSnapshot(BodyPartType.Work, 100, null), new CreepBodyPartSnapshot(BodyPartType.Work, 100, null)],
            energy: 100);
        var controller = CreateController("ctrl1", 11, 11, "user1",
            level: 1, progress: 50, downgradeTime: 50000);

        var argument1 = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: controller.Id)
        });
        var argument2 = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: controller.Id)
        });

        var record1 = new IntentRecord(IntentKeys.UpgradeController, [argument1]);
        var record2 = new IntentRecord(IntentKeys.UpgradeController, [argument2]);
        var objectIntents = new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal)
        {
            [creep.Id] = [record1, record2]
        };

        var envelope = new IntentEnvelope(
            "user1",
            objectIntents,
            new Dictionary<string, SpawnIntentEnvelope>(StringComparer.Ordinal),
            new Dictionary<string, CreepIntentEnvelope>(StringComparer.Ordinal));

        var users = new Dictionary<string, IntentEnvelope>(StringComparer.Ordinal)
        {
            ["user1"] = envelope
        };

        var intents = new RoomIntentSnapshot("W1N1", "shard0", users);
        var context = CreateContext([creep, controller], intents);
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == creep.Id && p.Payload.Store is not null);
        Assert.Equal(96, Payload.Store![ResourceTypes.Energy]);

        var ctrlPatch = writer.Patches.Single(p => p.ObjectId == controller.Id && p.Payload.Progress.HasValue);
        Assert.Equal(54, ctrlPatch.Payload.Progress);

        var stats = (FakeCreepStatsSink)context.Stats;
        Assert.Equal(4, stats.GetMetric("user1", RoomStatsMetricNames.EnergyControl));
    }

    [Fact]
    public async Task Upgrade_LevelUpTransition_IncrementsLevel()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1",
            body: [new CreepBodyPartSnapshot(BodyPartType.Work, 100, null)],
            energy: 50);
        var controller = CreateController("ctrl1", 11, 11, "user1",
            level: 1, progress: 199, downgradeTime: 25000);
        var context = CreateContext([creep, controller],
            CreateUpgradeIntent("user1", creep.Id, controller.Id), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var patches = writer.Patches.Where(p => p.ObjectId == controller.Id).ToList();
        var patch = patches[0].Payload;

        Assert.NotNull(patch);
        Assert.Equal(0, patch.Progress);
        Assert.True(patch.DowngradeTimer > 0);
        Assert.Equal(1, patch.SafeModeAvailable);

        Assert.NotNull(writer.RoomInfoPatch);
        Assert.Equal(ControllerLevel.Level2, writer.RoomInfoPatch!.ControllerLevel);
    }

    [Fact]
    public async Task Upgrade_Level8_EnforcesMaxPerTick()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1",
            body: Enumerable.Range(0, 20).Select(_ => new CreepBodyPartSnapshot(BodyPartType.Work, 100, null)).ToList(),
            energy: 100);
        var controller = CreateController("ctrl1", 11, 11, "user1",
            level: 8, progress: 100, downgradeTime: 100000);
        var context = CreateContext([creep, controller],
            CreateUpgradeIntent("user1", creep.Id, controller.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == creep.Id && p.Payload.Store is not null);
        Assert.Equal(85, Payload.Store![ResourceTypes.Energy]);

        var ctrlPatch = writer.Patches.Single(p => p.ObjectId == controller.Id && p.Payload.Progress.HasValue);
        Assert.Equal(115, ctrlPatch.Payload.Progress);

        var stats = (FakeCreepStatsSink)context.Stats;
        Assert.Equal(15, stats.GetMetric("user1", RoomStatsMetricNames.EnergyControl));
    }

    [Fact]
    public async Task Upgrade_InsufficientEnergy_Fails()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1",
            body: [new CreepBodyPartSnapshot(BodyPartType.Work, 100, null)],
            energy: 0);
        var controller = CreateController("ctrl1", 11, 11, "user1",
            level: 1, progress: 100, downgradeTime: 50000);
        var context = CreateContext([creep, controller],
            CreateUpgradeIntent("user1", creep.Id, controller.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Validation failures emit ActionLog on creep (standard pattern)
        var (objectId, payload) = Assert.Single(writer.Patches);
        Assert.Equal(creep.Id, objectId);
        Assert.NotNull(payload.ActionLog);
        Assert.False(payload.ActionLog.HasEntries);
    }

    [Fact]
    public async Task Upgrade_OutOfRange_Fails()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1",
            body: [new CreepBodyPartSnapshot(BodyPartType.Work, 100, null)],
            energy: 50);
        var controller = CreateController("ctrl1", 20, 20, "user1",
            level: 1, progress: 100, downgradeTime: 50000);
        var context = CreateContext([creep, controller],
            CreateUpgradeIntent("user1", creep.Id, controller.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Validation failures emit ActionLog on creep (standard pattern)
        var (objectId, payload) = Assert.Single(writer.Patches);
        Assert.Equal(creep.Id, objectId);
        Assert.NotNull(payload.ActionLog);
        Assert.False(payload.ActionLog.HasEntries);
    }

    [Fact]
    public async Task Upgrade_NotOwned_Fails()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1",
            body: [new CreepBodyPartSnapshot(BodyPartType.Work, 100, null)],
            energy: 50);
        var controller = CreateController("ctrl1", 11, 11, "user2",
            level: 1, progress: 100, downgradeTime: 50000);
        var context = CreateContext([creep, controller],
            CreateUpgradeIntent("user1", creep.Id, controller.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Validation failures emit ActionLog on creep (standard pattern)
        var (objectId, payload) = Assert.Single(writer.Patches);
        Assert.Equal(creep.Id, objectId);
        Assert.NotNull(payload.ActionLog);
        Assert.False(payload.ActionLog.HasEntries);
    }

    #endregion

    #region reserveController Tests

    [Fact]
    public async Task Reserve_NewReservation_CreatesReservation()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1",
            body: [new CreepBodyPartSnapshot(BodyPartType.Claim, 100, null)],
            energy: 0);
        var controller = CreateController("ctrl1", 11, 10, null,
            level: 0, progress: 0, downgradeTime: null);
        var context = CreateContext([creep, controller],
            CreateReserveIntent("user1", creep.Id, controller.Id), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == controller.Id && p.Payload.Reservation is not null);
        Assert.NotNull(Payload.Reservation);
        Assert.Equal("user1", Payload.Reservation!.UserId);
        Assert.Equal(101, Payload.Reservation.EndTime);
    }

    [Fact]
    public async Task Reserve_ExtendReservation_IncreasesEndTime()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1",
            body: [new CreepBodyPartSnapshot(BodyPartType.Claim, 100, null), new CreepBodyPartSnapshot(BodyPartType.Claim, 100, null)],
            energy: 0);
        var controller = CreateController("ctrl1", 11, 10, null,
            level: 0, progress: 0, downgradeTime: null,
            reservation: new RoomReservationSnapshot("user1", 200));
        var context = CreateContext([creep, controller],
            CreateReserveIntent("user1", creep.Id, controller.Id), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == controller.Id && p.Payload.Reservation is not null);
        Assert.NotNull(Payload.Reservation);
        Assert.Equal("user1", Payload.Reservation!.UserId);
        Assert.Equal(202, Payload.Reservation.EndTime);
    }

    [Fact]
    public async Task Reserve_ExceedsMax_Fails()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1",
            body: [new CreepBodyPartSnapshot(BodyPartType.Claim, 100, null)],
            energy: 0);
        var controller = CreateController("ctrl1", 11, 10, null,
            level: 0, progress: 0, downgradeTime: null,
            reservation: new RoomReservationSnapshot("user1", 5100));
        var context = CreateContext([creep, controller],
            CreateReserveIntent("user1", creep.Id, controller.Id), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(writer.Patches);
    }

    #endregion

    #region attackController Tests

    [Fact]
    public async Task Attack_ReducesReservation_DecrementsEndTime()
    {
        var invaderCore = CreateInvaderCore("core1", 10, 10, energy: 0);
        var controller = CreateController("ctrl1", 11, 10, null,
            level: 0, progress: 0, downgradeTime: null,
            reservation: new RoomReservationSnapshot("user1", 500));
        var context = CreateContext([invaderCore, controller],
            CreateAttackIntent("invader", invaderCore.Id, controller.Id), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var patches = writer.Patches.Where(p => p.ObjectId == controller.Id).ToList();
        Assert.NotEmpty(patches);

        var (ObjectId, Payload) = patches.FirstOrDefault(p => p.Payload.Reservation is not null);
        Assert.NotNull(ObjectId);
        Assert.Equal(200, Payload.Reservation!.EndTime);
    }

    [Fact]
    public async Task Attack_ReducesDowngrade_SetsUpgradeBlock()
    {
        var invaderCore = CreateInvaderCore("core1", 10, 10, energy: 0);
        var controller = CreateController("ctrl1", 11, 10, "user1",
            level: 3, progress: 1000, downgradeTime: 50000);
        var context = CreateContext([invaderCore, controller],
            CreateAttackIntent("invader", invaderCore.Id, controller.Id), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var patches = writer.Patches.Where(p => p.ObjectId == controller.Id).ToList();
        Assert.NotEmpty(patches);

        var (ObjectId, Payload) = patches.FirstOrDefault(p => p.Payload.DowngradeTimer.HasValue);
        Assert.NotNull(ObjectId);

        var blockPatch = patches.FirstOrDefault(p => p.Payload.UpgradeBlocked.HasValue);
        Assert.NotNull(blockPatch.ObjectId);
        Assert.True(blockPatch.Payload.UpgradeBlocked);
    }

    [Fact]
    public async Task Upgrade_NoBoost_AppliesNormalPower()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1",
            body: [new CreepBodyPartSnapshot(BodyPartType.Work, 100, null)],
            energy: 10);
        var controller = CreateController("ctrl1", 11, 11, "user1",
            level: 1, progress: 100, downgradeTime: 50000);
        var context = CreateContext([creep, controller],
            CreateUpgradeIntent("user1", creep.Id, controller.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == controller.Id && p.Payload.Progress.HasValue);
        Assert.Equal(101, Payload.Progress);
    }

    [Fact]
    public async Task Upgrade_WithGHBoost_AppliesBoostMultiplier()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1",
            body: [new CreepBodyPartSnapshot(BodyPartType.Work, 100, ResourceTypes.GhodiumHydride)],
            energy: 10);
        var controller = CreateController("ctrl1", 11, 11, "user1",
            level: 1, progress: 100, downgradeTime: 50000);
        var context = CreateContext([creep, controller],
            CreateUpgradeIntent("user1", creep.Id, controller.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == controller.Id && p.Payload.Progress.HasValue);
        Assert.Equal(100 + 1, Payload.Progress);
    }

    [Fact]
    public async Task Upgrade_MultipleBoosts_SortsAndAppliesHighestFirst()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1",
            body:
            [
                new CreepBodyPartSnapshot(BodyPartType.Work, 100, ResourceTypes.GhodiumHydride),
                new CreepBodyPartSnapshot(BodyPartType.Work, 100, ResourceTypes.CatalyzedGhodiumAcid),
                new CreepBodyPartSnapshot(BodyPartType.Work, 100, ResourceTypes.GhodiumAcid)
            ],
            energy: 2);
        var controller = CreateController("ctrl1", 11, 11, "user1",
            level: 1, progress: 100, downgradeTime: 50000);
        var context = CreateContext([creep, controller],
            CreateUpgradeIntent("user1", creep.Id, controller.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == controller.Id && p.Payload.Progress.HasValue);
        Assert.Equal(100 + 2 + 1, Payload.Progress);
    }

    [Fact]
    public async Task Upgrade_MixedParts_OnlyUsesBoostForWorkParts()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1",
            body:
            [
                new CreepBodyPartSnapshot(BodyPartType.Work, 100, ResourceTypes.CatalyzedGhodiumAcid),
                new CreepBodyPartSnapshot(BodyPartType.Move, 100, null),
                new CreepBodyPartSnapshot(BodyPartType.Work, 100, null)
            ],
            energy: 10);
        var controller = CreateController("ctrl1", 11, 11, "user1",
            level: 1, progress: 100, downgradeTime: 50000);
        var context = CreateContext([creep, controller],
            CreateUpgradeIntent("user1", creep.Id, controller.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == controller.Id && p.Payload.Progress.HasValue);
        Assert.Equal(100 + 2 + 1, Payload.Progress);
    }

    #endregion

    #region Helper Methods

    private static RoomProcessorContext CreateContext(
        IEnumerable<RoomObjectSnapshot> objects,
        RoomIntentSnapshot intents,
        int gameTime = 100,
        bool includeController = false)  // Default false since tests create their own controllers
    {
        var objectMap = objects.ToDictionary(o => o.Id, o => o, StringComparer.Ordinal);

        // Add controller for structure activation validation (RCL 8 by default)
        // NOTE: Most tests in this file create their own controllers, so default is false
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

        var state = new RoomState(
            "W1N1",
            gameTime,
            null,
            objectMap,
            new Dictionary<string, UserState>(StringComparer.Ordinal),
            intents,
            new Dictionary<string, RoomTerrainSnapshot>(StringComparer.Ordinal),
            []);

        return new RoomProcessorContext(state, new FakeMutationWriter(), new FakeCreepStatsSink(), new NullGlobalMutationWriter(), new NullNotificationSink());
    }

    private static RoomIntentSnapshot CreateUpgradeIntent(string userId, string creepId, string controllerId)
    {
        var argument = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: controllerId)
        });

        return CreateIntent(userId, creepId, IntentKeys.UpgradeController, argument);
    }

    private static RoomIntentSnapshot CreateReserveIntent(string userId, string creepId, string controllerId)
    {
        var argument = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: controllerId)
        });

        return CreateIntent(userId, creepId, IntentKeys.ReserveController, argument);
    }

    private static RoomIntentSnapshot CreateAttackIntent(string userId, string invaderCoreId, string controllerId)
    {
        var argument = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: controllerId)
        });

        return CreateIntent(userId, invaderCoreId, IntentKeys.AttackController, argument);
    }

    private static RoomIntentSnapshot CreateIntent(string userId, string objectId, string intentName, IntentArgument argument)
    {
        var record = new IntentRecord(intentName, [argument]);
        var objectIntents = new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal)
        {
            [objectId] = [record]
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
        IReadOnlyList<CreepBodyPartSnapshot> body,
        int energy)
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
            Store: new Dictionary<string, int>(StringComparer.Ordinal) { [ResourceTypes.Energy] = energy },
            StoreCapacity: 100,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: body,
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

    private static RoomObjectSnapshot CreateInvaderCore(string id, int x, int y, int energy)
        => new(
            id,
            RoomObjectTypes.InvaderCore,
            "W1N1",
            "shard0",
            null,
            x,
            y,
            Hits: 100000,
            HitsMax: 100000,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: 1,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.InvaderCore,
            Store: new Dictionary<string, int>(StringComparer.Ordinal) { [ResourceTypes.Energy] = energy },
            StoreCapacity: 5000,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: []);

    private static RoomObjectSnapshot CreateController(
        string id,
        int x,
        int y,
        string? userId,
        int level,
        int progress,
        int? downgradeTime,
        RoomReservationSnapshot? reservation = null)
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
            Level: level,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.Controller,
            Store: downgradeTime.HasValue
                ? new Dictionary<string, int>(StringComparer.Ordinal) { [RoomDocumentFields.RoomObject.DowngradeTimer] = downgradeTime.Value }
                : new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: reservation,
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
            Progress: progress,
            ProgressTotal: null,
            ActionLog: null,
            Energy: null,
            InvaderHarvested: null,
            MineralAmount: null,
            Harvested: null,
            Cooldown: null,
            CooldownTime: null);

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
        public bool IsMarkedForRemoval(string objectId) => false;

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

        public void IncrementEnergyControl(string userId, int amount)
            => Increment(userId, RoomStatsMetricNames.EnergyControl, amount);

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

        public int GetMetric(string userId, string metric)
            => _metrics.GetValueOrDefault(userId)?.GetValueOrDefault(metric, 0) ?? 0;

        private void Increment(string userId, string metric, int amount)
        {
            if (!_metrics.ContainsKey(userId))
                _metrics[userId] = [];

            _metrics[userId][metric] = _metrics[userId].GetValueOrDefault(metric, 0) + amount;
        }
    }

    #endregion
}
