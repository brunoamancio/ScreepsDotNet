using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.Bulk;
using ScreepsDotNet.Engine.Data.Models;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.Helpers;
using ScreepsDotNet.Engine.Processors.Steps;

namespace ScreepsDotNet.Engine.Tests.Processors.Steps;

public sealed class PowerAbilityStepTests
{
    [Fact]
    public async Task ExecuteAsync_ValidPowerUse_DeductsOpsAndSetsCooldown()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.OperateSpawn] = new(Level: 3, CooldownTime: 0)
            },
            ops: 150);
        var spawn = CreateSpawn("spawn1", 12, 10);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.OperateSpawn, "spawn1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, spawn, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        Assert.NotEmpty(writer.Patches);

        // Check ops deducted
        var powerCreepPatch = writer.Patches.FirstOrDefault(p => p.ObjectId == "pc1");
        Assert.NotNull(powerCreepPatch.Payload);
        Assert.NotNull(powerCreepPatch.Payload.Store);
        Assert.Equal(50, powerCreepPatch.Payload.Store[ResourceTypes.Ops]);

        // Check cooldown set (100 ops cost, 300 cooldown)
        Assert.NotNull(powerCreepPatch.Payload.Powers);
        Assert.True(powerCreepPatch.Payload.Powers.ContainsKey(PowerTypes.OperateSpawn));
        Assert.Equal(400, powerCreepPatch.Payload.Powers[PowerTypes.OperateSpawn].CooldownTime);
    }

    [Fact]
    public async Task ExecuteAsync_PowerNotEnabled_NoEffect()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.OperateSpawn] = new(Level: 3, CooldownTime: 0)
            },
            ops: 150);
        var spawn = CreateSpawn("spawn1", 12, 10);
        var controller = CreateController(isPowerEnabled: false);
        var intent = CreatePowerIntent(PowerTypes.OperateSpawn, "spawn1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, spawn, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(((RecordingMutationWriter)context.MutationWriter).Patches);
    }

    [Fact]
    public async Task ExecuteAsync_EnemySafeMode_NoEffect()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.OperateSpawn] = new(Level: 3, CooldownTime: 0)
            },
            ops: 150,
            userId: "user1");
        var spawn = CreateSpawn("spawn1", 12, 10);
        var controller = CreateController(isPowerEnabled: true, userId: "user2", safeMode: 200);
        var intent = CreatePowerIntent(PowerTypes.OperateSpawn, "spawn1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, spawn, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(((RecordingMutationWriter)context.MutationWriter).Patches);
    }

    [Fact]
    public async Task ExecuteAsync_PowerOnCooldown_NoEffect()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.OperateSpawn] = new(Level: 3, CooldownTime: 200)
            },
            ops: 150);
        var spawn = CreateSpawn("spawn1", 12, 10);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.OperateSpawn, "spawn1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, spawn, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(((RecordingMutationWriter)context.MutationWriter).Patches);
    }

    [Fact]
    public async Task ExecuteAsync_InsufficientOps_NoEffect()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.OperateSpawn] = new(Level: 3, CooldownTime: 0)
            },
            ops: 50);
        var spawn = CreateSpawn("spawn1", 12, 10);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.OperateSpawn, "spawn1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, spawn, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(((RecordingMutationWriter)context.MutationWriter).Patches);
    }

    [Fact]
    public async Task ExecuteAsync_TargetOutOfRange_NoEffect()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.OperateSpawn] = new(Level: 3, CooldownTime: 0)
            },
            ops: 150);
        var spawn = CreateSpawn("spawn1", 20, 20);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.OperateSpawn, "spawn1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, spawn, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(((RecordingMutationWriter)context.MutationWriter).Patches);
    }

    [Fact]
    public async Task ExecuteAsync_TargetHasHigherLevelEffect_NoEffect()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.OperateSpawn] = new(Level: 2, CooldownTime: 0)
            },
            ops: 150);
        var spawn = CreateSpawn("spawn1", 12, 10, effects: new Dictionary<PowerTypes, PowerEffectSnapshot>
        {
            [PowerTypes.OperateSpawn] = new(PowerTypes.OperateSpawn, Level: 4, EndTime: 200)
        });
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.OperateSpawn, "spawn1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, spawn, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(((RecordingMutationWriter)context.MutationWriter).Patches);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownPower_NoEffect()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>(),
            ops: 150);
        var spawn = CreateSpawn("spawn1", 12, 10);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent((PowerTypes)999, "spawn1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, spawn, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(((RecordingMutationWriter)context.MutationWriter).Patches);
    }

    [Fact]
    public async Task ExecuteAsync_PowerCreepLacksAbility_NoEffect()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.OperateTower] = new(Level: 3, CooldownTime: 0)
            },
            ops: 150);
        var spawn = CreateSpawn("spawn1", 12, 10);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.OperateSpawn, "spawn1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, spawn, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(((RecordingMutationWriter)context.MutationWriter).Patches);
    }

    [Fact]
    public async Task ExecuteAsync_ActionLogRecorded()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.OperateSpawn] = new(Level: 3, CooldownTime: 0)
            },
            ops: 150);
        var spawn = CreateSpawn("spawn1", 12, 10);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.OperateSpawn, "spawn1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, spawn, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var powerCreepPatch = writer.Patches.FirstOrDefault(p => p.ObjectId == "pc1");
        Assert.NotNull(powerCreepPatch.Payload.ActionLog);
        Assert.NotNull(powerCreepPatch.Payload.ActionLog.UsePower);
        Assert.Equal((int)PowerTypes.OperateSpawn, powerCreepPatch.Payload.ActionLog.UsePower.Power);
        Assert.Equal(12, powerCreepPatch.Payload.ActionLog.UsePower.X);
        Assert.Equal(10, powerCreepPatch.Payload.ActionLog.UsePower.Y);
    }

    private static RoomProcessorContext CreateContext(RoomObjectSnapshot powerCreep, int gameTime, RoomObjectSnapshot controller, RoomObjectSnapshot target, IReadOnlyDictionary<string, IReadOnlyList<IntentRecord>> objectIntents)
    {
        var objects = new Dictionary<string, RoomObjectSnapshot>(StringComparer.Ordinal)
        {
            [powerCreep.Id] = powerCreep,
            [controller.Id] = controller,
            [target.Id] = target
        };

        var intents = new RoomIntentSnapshot(
            powerCreep.RoomName,
            null,
            new Dictionary<string, IntentEnvelope>(StringComparer.Ordinal)
            {
                [powerCreep.UserId!] = new(powerCreep.UserId!,
                                           objectIntents,
                                           new Dictionary<string, SpawnIntentEnvelope>(StringComparer.Ordinal),
                                           new Dictionary<string, CreepIntentEnvelope>(StringComparer.Ordinal))
            });

        var state = new RoomState(
            powerCreep.RoomName,
            gameTime,
            null,
            objects,
            new Dictionary<string, UserState>(StringComparer.Ordinal),
            intents,
            new Dictionary<string, RoomTerrainSnapshot>(StringComparer.Ordinal),
            []);

        return new RoomProcessorContext(
            state,
            new RecordingMutationWriter(),
            new NullCreepStatsSink());
    }

    private static RoomObjectSnapshot CreatePowerCreep(IReadOnlyDictionary<PowerTypes, PowerCreepPowerSnapshot> powers, int ops, string userId = "user1")
        => new(
            "pc1",
            RoomObjectTypes.PowerCreep,
            "W1N1",
            null,
            userId,
            10,
            10,
            Hits: 1000,
            HitsMax: 1000,
            Fatigue: 0,
            TicksToLive: null,
            Name: "PC1",
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: null,
            Store: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [ResourceTypes.Ops] = ops
            },
            StoreCapacity: 100,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: [],
            Powers: powers);

    private static RoomObjectSnapshot CreateSpawn(string id, int x, int y, IReadOnlyDictionary<PowerTypes, PowerEffectSnapshot>? effects = null)
        => new(
            id,
            RoomObjectTypes.Spawn,
            "W1N1",
            null,
            "user1",
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
            StructureType: null,
            Store: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [ResourceTypes.Energy] = 300
            },
            StoreCapacity: 300,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: effects ?? new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: []);

    private static RoomObjectSnapshot CreateController(bool isPowerEnabled, string userId = "user1", int? safeMode = null)
        => new(
            "controller1",
            RoomObjectTypes.Controller,
            "W1N1",
            null,
            userId,
            25,
            25,
            Hits: 0,
            HitsMax: 0,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: 5,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: null,
            Store: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [RoomDocumentFields.Controller.IsPowerEnabled] = isPowerEnabled ? 1 : 0
            },
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: [],
            SafeMode: safeMode);

    private static IReadOnlyDictionary<string, IReadOnlyList<IntentRecord>> CreatePowerIntent(PowerTypes power, string targetId)
    {
        var intent = new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal)
        {
            ["pc1"] = new List<IntentRecord>
            {
                new(
                    IntentKeys.Power,
                    new List<IntentArgument>
                    {
                        new(
                            new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
                            {
                                [PowerCreepIntentFields.Power] = new(IntentFieldValueKind.Number, NumberValue: (int)power),
                                [PowerCreepIntentFields.Id] = new(IntentFieldValueKind.Text, TextValue: targetId)
                            })
                    })
            }
        };
        return intent;
    }

    private sealed class RecordingMutationWriter : IRoomMutationWriter
    {
        public List<(string ObjectId, RoomObjectPatchPayload Payload)> Patches { get; } = [];

        public void Upsert(RoomObjectSnapshot document) { }

        public void Patch(string objectId, RoomObjectPatchPayload patch)
            => Patches.Add((objectId, patch));

        public void Remove(string objectId) { }

        public void SetRoomInfoPatch(RoomInfoPatchPayload patch) { }

        public void SetEventLog(IRoomEventLogPayload? eventLog) { }

        public void SetMapView(IRoomMapViewPayload? mapView) { }

        public Task FlushAsync(CancellationToken token = default) => Task.CompletedTask;

        public void Reset()
            => Patches.Clear();
    }
}
