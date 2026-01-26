using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.Bulk;
using ScreepsDotNet.Engine.Data.Models;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.Helpers;
using ScreepsDotNet.Engine.Processors.Steps;
using ScreepsDotNet.Engine.Tests.Processors.Helpers;

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
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "pc1");
        Assert.NotNull(Payload);
        Assert.NotNull(Payload.Store);
        Assert.Equal(50, Payload.Store[ResourceTypes.Ops]);

        // Check cooldown set (100 ops cost, 300 cooldown)
        Assert.NotNull(Payload.Powers);
        Assert.True(Payload.Powers.ContainsKey(PowerTypes.OperateSpawn));
        Assert.Equal(400, Payload.Powers[PowerTypes.OperateSpawn].CooldownTime);
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
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "pc1");
        Assert.NotNull(Payload.ActionLog);
        Assert.NotNull(Payload.ActionLog.UsePower);
        Assert.Equal((int)PowerTypes.OperateSpawn, Payload.ActionLog.UsePower.Power);
        Assert.Equal(12, Payload.ActionLog.UsePower.X);
        Assert.Equal(10, Payload.ActionLog.UsePower.Y);
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
            new NullCreepStatsSink(),
            new NullGlobalMutationWriter(),
            new NullNotificationSink());
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

    private static RoomObjectSnapshot CreateController(bool isPowerEnabled, string userId = "user1", int? safeMode = null, int x = 25, int y = 25)
        => new(
            "controller1",
            RoomObjectTypes.Controller,
            "W1N1",
            null,
            userId,
            x,
            y,
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

    [Fact]
    public async Task ExecuteAsync_OperateSpawn_AppliesEffectToSpawn()
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
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "spawn1");
        Assert.NotNull(Payload.Effects);
        Assert.True(Payload.Effects.ContainsKey(PowerTypes.OperateSpawn));
        Assert.Equal(3, Payload.Effects[PowerTypes.OperateSpawn].Level);
        Assert.Equal(1100, Payload.Effects[PowerTypes.OperateSpawn].EndTime);
    }

    [Fact]
    public async Task ExecuteAsync_OperateSpawn_WrongTargetType_NoEffect()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.OperateSpawn] = new(Level: 3, CooldownTime: 0)
            },
            ops: 150);
        var tower = CreateTower("tower1", 12, 10);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.OperateSpawn, "tower1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, tower, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "tower1");
        Assert.True(ObjectId is null || Payload.Effects is null);
    }

    [Fact]
    public async Task ExecuteAsync_OperateTower_AppliesEffectToTower()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.OperateTower] = new(Level: 2, CooldownTime: 0)
            },
            ops: 50);
        var tower = CreateTower("tower1", 12, 10);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.OperateTower, "tower1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, tower, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "tower1");
        Assert.NotNull(Payload.Effects);
        Assert.True(Payload.Effects.ContainsKey(PowerTypes.OperateTower));
        Assert.Equal(2, Payload.Effects[PowerTypes.OperateTower].Level);
        Assert.Equal(200, Payload.Effects[PowerTypes.OperateTower].EndTime);
    }

    [Fact]
    public async Task ExecuteAsync_OperateTower_WrongTargetType_NoEffect()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.OperateTower] = new(Level: 2, CooldownTime: 0)
            },
            ops: 50);
        var spawn = CreateSpawn("spawn1", 12, 10);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.OperateTower, "spawn1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, spawn, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "spawn1");
        Assert.True(ObjectId is null || Payload.Effects is null);
    }

    [Fact]
    public async Task ExecuteAsync_OperateStorage_AppliesEffectToStorage()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.OperateStorage] = new(Level: 1, CooldownTime: 0)
            },
            ops: 150);
        var storage = CreateStorage("storage1", 12, 10);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.OperateStorage, "storage1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, storage, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "storage1");
        Assert.NotNull(Payload.Effects);
        Assert.True(Payload.Effects.ContainsKey(PowerTypes.OperateStorage));
        Assert.Equal(1, Payload.Effects[PowerTypes.OperateStorage].Level);
    }

    [Fact]
    public async Task ExecuteAsync_OperateStorage_WrongTargetType_NoEffect()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.OperateStorage] = new(Level: 1, CooldownTime: 0)
            },
            ops: 150);
        var spawn = CreateSpawn("spawn1", 12, 10);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.OperateStorage, "spawn1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, spawn, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "spawn1");
        Assert.True(ObjectId is null || Payload.Effects is null || Payload.Effects.Count == 0);
    }

    [Fact]
    public async Task ExecuteAsync_OperateLab_AppliesEffectToLab()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.OperateLab] = new(Level: 4, CooldownTime: 0)
            },
            ops: 50);
        var lab = CreateLab("lab1", 12, 10);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.OperateLab, "lab1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, lab, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "lab1");
        Assert.NotNull(Payload.Effects);
        Assert.True(Payload.Effects.ContainsKey(PowerTypes.OperateLab));
        Assert.Equal(4, Payload.Effects[PowerTypes.OperateLab].Level);
    }

    [Fact]
    public async Task ExecuteAsync_OperateLab_WrongTargetType_NoEffect()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.OperateLab] = new(Level: 4, CooldownTime: 0)
            },
            ops: 50);
        var spawn = CreateSpawn("spawn1", 12, 10);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.OperateLab, "spawn1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, spawn, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "spawn1");
        Assert.True(ObjectId is null || Payload.Effects is null || Payload.Effects.Count == 0);
    }

    [Fact]
    public async Task ExecuteAsync_OperateObserver_AppliesEffectToObserver()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.OperateObserver] = new(Level: 5, CooldownTime: 0)
            },
            ops: 50);
        var observer = CreateObserver("observer1", 12, 10);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.OperateObserver, "observer1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, observer, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "observer1");
        Assert.NotNull(Payload.Effects);
        Assert.True(Payload.Effects.ContainsKey(PowerTypes.OperateObserver));
        Assert.Equal(5, Payload.Effects[PowerTypes.OperateObserver].Level);
    }

    [Fact]
    public async Task ExecuteAsync_OperateObserver_WrongTargetType_NoEffect()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.OperateObserver] = new(Level: 5, CooldownTime: 0)
            },
            ops: 50);
        var spawn = CreateSpawn("spawn1", 12, 10);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.OperateObserver, "spawn1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, spawn, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "spawn1");
        Assert.True(ObjectId is null || Payload.Effects is null || Payload.Effects.Count == 0);
    }

    [Fact]
    public async Task ExecuteAsync_OperateTerminal_AppliesEffectToTerminal()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.OperateTerminal] = new(Level: 2, CooldownTime: 0)
            },
            ops: 150);
        var terminal = CreateTerminal("terminal1", 12, 10);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.OperateTerminal, "terminal1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, terminal, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "terminal1");
        Assert.NotNull(Payload.Effects);
        Assert.True(Payload.Effects.ContainsKey(PowerTypes.OperateTerminal));
        Assert.Equal(2, Payload.Effects[PowerTypes.OperateTerminal].Level);
    }

    [Fact]
    public async Task ExecuteAsync_OperateTerminal_WrongTargetType_NoEffect()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.OperateTerminal] = new(Level: 2, CooldownTime: 0)
            },
            ops: 150);
        var spawn = CreateSpawn("spawn1", 12, 10);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.OperateTerminal, "spawn1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, spawn, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "spawn1");
        Assert.True(ObjectId is null || Payload.Effects is null || Payload.Effects.Count == 0);
    }

    [Fact]
    public async Task ExecuteAsync_OperatePower_AppliesEffectToPowerSpawn()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.OperatePower] = new(Level: 3, CooldownTime: 0)
            },
            ops: 250);
        var powerSpawn = CreatePowerSpawn("powerSpawn1", 12, 10);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.OperatePower, "powerSpawn1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, powerSpawn, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "powerSpawn1");
        Assert.NotNull(Payload.Effects);
        Assert.True(Payload.Effects.ContainsKey(PowerTypes.OperatePower));
        Assert.Equal(3, Payload.Effects[PowerTypes.OperatePower].Level);
    }

    [Fact]
    public async Task ExecuteAsync_OperatePower_WrongTargetType_NoEffect()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.OperatePower] = new(Level: 3, CooldownTime: 0)
            },
            ops: 250);
        var spawn = CreateSpawn("spawn1", 12, 10);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.OperatePower, "spawn1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, spawn, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "spawn1");
        Assert.True(ObjectId is null || Payload.Effects is null || Payload.Effects.Count == 0);
    }

    [Fact]
    public async Task ExecuteAsync_OperateController_AppliesEffectToController()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.OperateController] = new(Level: 4, CooldownTime: 0)
            },
            ops: 250);
        var controller = CreateController(isPowerEnabled: true, x: 12, y: 10);
        var intent = CreatePowerIntent(PowerTypes.OperateController, "controller1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, controller, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "controller1");
        Assert.NotNull(Payload.Effects);
        Assert.True(Payload.Effects.ContainsKey(PowerTypes.OperateController));
        Assert.Equal(4, Payload.Effects[PowerTypes.OperateController].Level);
    }

    [Fact]
    public async Task ExecuteAsync_OperateController_WrongTargetType_NoEffect()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.OperateController] = new(Level: 4, CooldownTime: 0)
            },
            ops: 250);
        var spawn = CreateSpawn("spawn1", 12, 10);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.OperateController, "spawn1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, spawn, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "spawn1");
        Assert.True(ObjectId is null || Payload.Effects is null || Payload.Effects.Count == 0);
    }

    // Group 2: Disruption Effects (8 tests)

    [Fact]
    public async Task ExecuteAsync_DisruptSpawn_AppliesEffectToSpawn()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.DisruptSpawn] = new(Level: 3, CooldownTime: 0)
            },
            ops: 50);
        var spawn = CreateSpawn("spawn1", 12, 10);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.DisruptSpawn, "spawn1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, spawn, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "spawn1");
        Assert.NotNull(Payload.Effects);
        Assert.True(Payload.Effects.ContainsKey(PowerTypes.DisruptSpawn));
        Assert.Equal(3, Payload.Effects[PowerTypes.DisruptSpawn].Level);
    }

    [Fact]
    public async Task ExecuteAsync_DisruptSpawn_WrongTargetType_NoEffect()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.DisruptSpawn] = new(Level: 3, CooldownTime: 0)
            },
            ops: 50);
        var tower = CreateTower("tower1", 12, 10);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.DisruptSpawn, "tower1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, tower, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "tower1");
        Assert.True(ObjectId is null || Payload.Effects is null || Payload.Effects.Count == 0);
    }

    [Fact]
    public async Task ExecuteAsync_DisruptTower_AppliesEffectToTower()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.DisruptTower] = new(Level: 2, CooldownTime: 0)
            },
            ops: 50);
        var tower = CreateTower("tower1", 30, 30);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.DisruptTower, "tower1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, tower, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "tower1");
        Assert.NotNull(Payload.Effects);
        Assert.True(Payload.Effects.ContainsKey(PowerTypes.DisruptTower));
        Assert.Equal(2, Payload.Effects[PowerTypes.DisruptTower].Level);
    }

    [Fact]
    public async Task ExecuteAsync_DisruptTower_WrongTargetType_NoEffect()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.DisruptTower] = new(Level: 2, CooldownTime: 0)
            },
            ops: 50);
        var spawn = CreateSpawn("spawn1", 30, 30);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.DisruptTower, "spawn1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, spawn, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "spawn1");
        Assert.True(ObjectId is null || Payload.Effects is null || Payload.Effects.Count == 0);
    }

    [Fact]
    public async Task ExecuteAsync_DisruptSource_AppliesEffectToSource()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.DisruptSource] = new(Level: 4, CooldownTime: 0)
            },
            ops: 150);
        var source = CreateSource("source1", 12, 10);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.DisruptSource, "source1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, source, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "source1");
        Assert.NotNull(Payload.Effects);
        Assert.True(Payload.Effects.ContainsKey(PowerTypes.DisruptSource));
        Assert.Equal(4, Payload.Effects[PowerTypes.DisruptSource].Level);
    }

    [Fact]
    public async Task ExecuteAsync_DisruptSource_WrongTargetType_NoEffect()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.DisruptSource] = new(Level: 4, CooldownTime: 0)
            },
            ops: 150);
        var spawn = CreateSpawn("spawn1", 12, 10);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.DisruptSource, "spawn1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, spawn, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "spawn1");
        Assert.True(ObjectId is null || Payload.Effects is null || Payload.Effects.Count == 0);
    }

    [Fact]
    public async Task ExecuteAsync_DisruptTerminal_AppliesEffectToTerminal()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.DisruptTerminal] = new(Level: 3, CooldownTime: 0)
            },
            ops: 100);
        var terminal = CreateTerminal("terminal1", 30, 30);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.DisruptTerminal, "terminal1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, terminal, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "terminal1");
        Assert.NotNull(Payload.Effects);
        Assert.True(Payload.Effects.ContainsKey(PowerTypes.DisruptTerminal));
        Assert.Equal(3, Payload.Effects[PowerTypes.DisruptTerminal].Level);
    }

    [Fact]
    public async Task ExecuteAsync_DisruptTerminal_WrongTargetType_NoEffect()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.DisruptTerminal] = new(Level: 3, CooldownTime: 0)
            },
            ops: 100);
        var spawn = CreateSpawn("spawn1", 30, 30);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.DisruptTerminal, "spawn1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, spawn, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "spawn1");
        Assert.True(ObjectId is null || Payload.Effects is null || Payload.Effects.Count == 0);
    }

    // Group 3: Regeneration Effects (4 tests)

    [Fact]
    public async Task ExecuteAsync_RegenSource_AppliesEffectToSource()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.RegenSource] = new(Level: 5, CooldownTime: 0)
            },
            ops: 150);
        var source = CreateSource("source1", 12, 10);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.RegenSource, "source1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, source, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "source1");
        Assert.NotNull(Payload.Effects);
        Assert.True(Payload.Effects.ContainsKey(PowerTypes.RegenSource));
        Assert.Equal(5, Payload.Effects[PowerTypes.RegenSource].Level);
    }

    [Fact]
    public async Task ExecuteAsync_RegenSource_WrongTargetType_NoEffect()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.RegenSource] = new(Level: 5, CooldownTime: 0)
            },
            ops: 150);
        var spawn = CreateSpawn("spawn1", 12, 10);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.RegenSource, "spawn1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, spawn, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "spawn1");
        Assert.True(ObjectId is null || Payload.Effects is null || Payload.Effects.Count == 0);
    }

    [Fact]
    public async Task ExecuteAsync_RegenMineral_AppliesEffectToMineral()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.RegenMineral] = new(Level: 3, CooldownTime: 0)
            },
            ops: 150);
        var mineral = CreateMineral("mineral1", 12, 10, mineralAmount: 5000);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.RegenMineral, "mineral1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, mineral, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "mineral1");
        Assert.NotNull(Payload.Effects);
        Assert.True(Payload.Effects.ContainsKey(PowerTypes.RegenMineral));
        Assert.Equal(3, Payload.Effects[PowerTypes.RegenMineral].Level);
    }

    [Fact]
    public async Task ExecuteAsync_RegenMineral_WrongTargetType_NoEffect()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.RegenMineral] = new(Level: 3, CooldownTime: 0)
            },
            ops: 150);
        var spawn = CreateSpawn("spawn1", 12, 10);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.RegenMineral, "spawn1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, spawn, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "spawn1");
        Assert.True(ObjectId is null || Payload.Effects is null || Payload.Effects.Count == 0);
    }

    // Group 4: Fortify (2 tests)

    [Fact]
    public async Task ExecuteAsync_Fortify_AppliesEffectToWall()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.Fortify] = new(Level: 4, CooldownTime: 0)
            },
            ops: 50);
        var wall = CreateWall("wall1", 12, 10);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.Fortify, "wall1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, wall, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "wall1");
        Assert.NotNull(Payload.Effects);
        Assert.True(Payload.Effects.ContainsKey(PowerTypes.Fortify));
        Assert.Equal(4, Payload.Effects[PowerTypes.Fortify].Level);
    }

    [Fact]
    public async Task ExecuteAsync_Fortify_WrongTargetType_NoEffect()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.Fortify] = new(Level: 4, CooldownTime: 0)
            },
            ops: 50);
        var spawn = CreateSpawn("spawn1", 12, 10);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.Fortify, "spawn1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, spawn, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "spawn1");
        Assert.True(ObjectId is null || Payload.Effects is null || Payload.Effects.Count == 0);
    }

    // Group 5: Factory (2 tests)

    [Fact]
    public async Task ExecuteAsync_OperateFactory_AppliesEffectToFactory()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.OperateFactory] = new(Level: 2, CooldownTime: 0)
            },
            ops: 150);
        var factory = CreateFactory("factory1", 12, 10);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.OperateFactory, "factory1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, factory, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "factory1");
        Assert.NotNull(Payload.Effects);
        Assert.True(Payload.Effects.ContainsKey(PowerTypes.OperateFactory));
        Assert.Equal(2, Payload.Effects[PowerTypes.OperateFactory].Level);
    }

    [Fact]
    public async Task ExecuteAsync_OperateFactory_WrongTargetType_NoEffect()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.OperateFactory] = new(Level: 2, CooldownTime: 0)
            },
            ops: 150);
        var spawn = CreateSpawn("spawn1", 12, 10);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.OperateFactory, "spawn1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, spawn, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "spawn1");
        Assert.True(ObjectId is null || Payload.Effects is null || Payload.Effects.Count == 0);
    }

    [Fact]
    public async Task ExecuteAsync_OperateExtension_FillsExtensionsFromStorage()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.OperateExtension] = new(Level: 3, CooldownTime: 0)
            },
            ops: 50);
        var storage = CreateStorage("storage1", 12, 10);
        storage = storage with { Store = new Dictionary<string, int>(StringComparer.Ordinal) { [ResourceTypes.Energy] = 10000 } };
        var extension1 = CreateExtension("ext1", 15, 10);
        var extension2 = CreateExtension("ext2", 16, 10);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.OperateExtension, "storage1");
        var context = CreateContextWithMultipleObjects(powerCreep, gameTime: 100, controller, [storage, extension1, extension2], intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "storage1");
        var ext1Patch = writer.Patches.FirstOrDefault(p => p.ObjectId == "ext1");
        var ext2Patch = writer.Patches.FirstOrDefault(p => p.ObjectId == "ext2");
        Assert.NotNull(Payload.Store);
        Assert.True(Payload.Store[ResourceTypes.Energy] < 10000);
        Assert.NotNull(ext1Patch.Payload.Store);
        Assert.True(ext1Patch.Payload.Store[ResourceTypes.Energy] > 0);
        Assert.NotNull(ext2Patch.Payload.Store);
        Assert.True(ext2Patch.Payload.Store[ResourceTypes.Energy] > 0);
    }

    [Fact]
    public async Task ExecuteAsync_OperateExtension_InsufficientStorageEnergy_NoEffect()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.OperateExtension] = new(Level: 3, CooldownTime: 0)
            },
            ops: 50);
        var storage = CreateStorage("storage1", 12, 10);
        storage = storage with { Store = new Dictionary<string, int>(StringComparer.Ordinal) { [ResourceTypes.Energy] = 0 } };
        var extension1 = CreateExtension("ext1", 15, 10);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.OperateExtension, "storage1");
        var context = CreateContextWithMultipleObjects(powerCreep, gameTime: 100, controller, [storage, extension1], intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "ext1");
        Assert.True(ObjectId is null || Payload.Store is null);
    }

    [Fact]
    public async Task ExecuteAsync_OperateExtension_WrongTargetType_NoEffect()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.OperateExtension] = new(Level: 3, CooldownTime: 0)
            },
            ops: 50);
        var spawn = CreateSpawn("spawn1", 12, 10);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.OperateExtension, "spawn1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, spawn, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "pc1");
        Assert.True(ObjectId is null || Payload.Store is null || Payload.Store[ResourceTypes.Ops] == 50);
    }

    [Fact]
    public async Task ExecuteAsync_OperateExtension_NoExtensionsInRoom_NoEnergyTransferred()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.OperateExtension] = new(Level: 3, CooldownTime: 0)
            },
            ops: 50);
        var storage = CreateStorage("storage1", 12, 10);
        storage = storage with { Store = new Dictionary<string, int>(StringComparer.Ordinal) { [ResourceTypes.Energy] = 10000 } };
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.OperateExtension, "storage1");
        var context = CreateContext(powerCreep, gameTime: 100, controller, storage, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "storage1");
        Assert.True(ObjectId is null || Payload.Store is null || Payload.Store[ResourceTypes.Energy] == 10000);
    }

    [Fact]
    public async Task ExecuteAsync_OperateExtension_FillsOnlyNonFullExtensions()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.OperateExtension] = new(Level: 3, CooldownTime: 0)
            },
            ops: 50);
        var storage = CreateStorage("storage1", 12, 10);
        storage = storage with { Store = new Dictionary<string, int>(StringComparer.Ordinal) { [ResourceTypes.Energy] = 10000 } };
        var extension1 = CreateExtension("ext1", 15, 10);
        var extension2 = CreateExtension("ext2", 16, 10);
        extension2 = extension2 with { Store = new Dictionary<string, int>(StringComparer.Ordinal) { [ResourceTypes.Energy] = 50 }, StoreCapacity = 50 };
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.OperateExtension, "storage1");
        var context = CreateContextWithMultipleObjects(powerCreep, gameTime: 100, controller, [storage, extension1, extension2], intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "ext1");
        var ext2Patch = writer.Patches.FirstOrDefault(p => p.ObjectId == "ext2");
        Assert.NotNull(Payload.Store);
        Assert.True(Payload.Store[ResourceTypes.Energy] > 0);
        Assert.True(ext2Patch.ObjectId is null || ext2Patch.Payload.Store is null);
    }

    [Fact]
    public async Task ExecuteAsync_OperateExtension_UsesTerminalIfNoStorage()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.OperateExtension] = new(Level: 3, CooldownTime: 0)
            },
            ops: 50);
        var terminal = CreateTerminal("terminal1", 12, 10);
        terminal = terminal with { Store = new Dictionary<string, int>(StringComparer.Ordinal) { [ResourceTypes.Energy] = 10000 } };
        var extension1 = CreateExtension("ext1", 15, 10);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntent(PowerTypes.OperateExtension, "terminal1");
        var context = CreateContextWithMultipleObjects(powerCreep, gameTime: 100, controller, [terminal, extension1], intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "terminal1");
        var ext1Patch = writer.Patches.FirstOrDefault(p => p.ObjectId == "ext1");
        Assert.NotNull(Payload.Store);
        Assert.True(Payload.Store[ResourceTypes.Energy] < 10000);
        Assert.NotNull(ext1Patch.Payload.Store);
        Assert.True(ext1Patch.Payload.Store[ResourceTypes.Energy] > 0);
    }

    // shield (6 tests)

    [Fact]
    public async Task ExecuteAsync_Shield_CreatesTemporaryRampart()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.Shield] = new(Level: 3, CooldownTime: 0)
            },
            ops: 50);
        powerCreep = powerCreep with { Store = new Dictionary<string, int>(StringComparer.Ordinal) { [ResourceTypes.Energy] = 200, [ResourceTypes.Ops] = 50 } };
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntentNoTarget(PowerTypes.Shield);
        var context = CreateContext(powerCreep, gameTime: 100, controller, controller, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var rampartUpserts = writer.Upserts.Where(u => string.Equals(u.Type, RoomObjectTypes.Rampart, StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(rampartUpserts);
        var rampart = rampartUpserts.First();
        Assert.Equal(10, rampart.X);
        Assert.Equal(10, rampart.Y);
        Assert.Equal(15000, rampart.Hits);
    }

    [Fact]
    public async Task ExecuteAsync_Shield_InsufficientEnergy_NoRampart()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.Shield] = new(Level: 3, CooldownTime: 0)
            },
            ops: 50);
        powerCreep = powerCreep with { Store = new Dictionary<string, int>(StringComparer.Ordinal) { [ResourceTypes.Energy] = 50, [ResourceTypes.Ops] = 50 } };
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntentNoTarget(PowerTypes.Shield);
        var context = CreateContext(powerCreep, gameTime: 100, controller, controller, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var rampartUpserts = writer.Upserts.Where(u => string.Equals(u.Type, RoomObjectTypes.Rampart, StringComparison.Ordinal)).ToList();
        Assert.Empty(rampartUpserts);
    }

    [Fact]
    public async Task ExecuteAsync_Shield_DeductsEnergy()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.Shield] = new(Level: 3, CooldownTime: 0)
            },
            ops: 50);
        powerCreep = powerCreep with { Store = new Dictionary<string, int>(StringComparer.Ordinal) { [ResourceTypes.Energy] = 200, [ResourceTypes.Ops] = 50 } };
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntentNoTarget(PowerTypes.Shield);
        var context = CreateContext(powerCreep, gameTime: 100, controller, controller, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "pc1");
        Assert.NotNull(Payload.Store);
        Assert.Equal(100, Payload.Store[ResourceTypes.Energy]);
    }

    [Fact]
    public async Task ExecuteAsync_Shield_ExistingRampartAtPosition_NoNewRampart()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.Shield] = new(Level: 3, CooldownTime: 0)
            },
            ops: 50);
        powerCreep = powerCreep with { Store = new Dictionary<string, int>(StringComparer.Ordinal) { [ResourceTypes.Energy] = 200, [ResourceTypes.Ops] = 50 } };
        var existingRampart = CreateRampart("rampart1", 10, 10);
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntentNoTarget(PowerTypes.Shield);
        var context = CreateContextWithMultipleObjects(powerCreep, gameTime: 100, controller, [existingRampart], intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var rampartUpserts = writer.Upserts.Where(u => string.Equals(u.Type, RoomObjectTypes.Rampart, StringComparison.Ordinal)).ToList();
        Assert.Empty(rampartUpserts);
    }

    [Fact]
    public async Task ExecuteAsync_Shield_SetsCooldown()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.Shield] = new(Level: 3, CooldownTime: 0)
            },
            ops: 50);
        powerCreep = powerCreep with { Store = new Dictionary<string, int>(StringComparer.Ordinal) { [ResourceTypes.Energy] = 200, [ResourceTypes.Ops] = 50 } };
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntentNoTarget(PowerTypes.Shield);
        var context = CreateContext(powerCreep, gameTime: 100, controller, controller, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (ObjectId, Payload) = writer.Patches.FirstOrDefault(p => p.ObjectId == "pc1");
        Assert.NotNull(Payload.Powers);
        Assert.True(Payload.Powers.ContainsKey(PowerTypes.Shield));
        Assert.Equal(120, Payload.Powers[PowerTypes.Shield].CooldownTime);
    }

    [Fact]
    public async Task ExecuteAsync_Shield_SetsDecayTime()
    {
        var powerCreep = CreatePowerCreep(
            powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>
            {
                [PowerTypes.Shield] = new(Level: 3, CooldownTime: 0)
            },
            ops: 50);
        powerCreep = powerCreep with { Store = new Dictionary<string, int>(StringComparer.Ordinal) { [ResourceTypes.Energy] = 200, [ResourceTypes.Ops] = 50 } };
        var controller = CreateController(isPowerEnabled: true);
        var intent = CreatePowerIntentNoTarget(PowerTypes.Shield);
        var context = CreateContext(powerCreep, gameTime: 100, controller, controller, intent);
        var step = new PowerAbilityStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var writer = (RecordingMutationWriter)context.MutationWriter;
        var rampartUpserts = writer.Upserts.Where(u => string.Equals(u.Type, RoomObjectTypes.Rampart, StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(rampartUpserts);
        var rampart = rampartUpserts.First();
        Assert.Equal(150, rampart.DecayTime);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<IntentRecord>> CreatePowerIntentNoTarget(PowerTypes power)
    {
        var intent = new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal)
        {
            ["pc1"] =
            [
                new(
                    IntentKeys.UsePower,
                    [
                        new(
                            new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
                            {
                                [PowerCreepIntentFields.Power] = new(IntentFieldValueKind.Number, NumberValue: (int)power)
                            })
                    ])
            ]
        };
        return intent;
    }

    private static RoomProcessorContext CreateContextWithMultipleObjects(RoomObjectSnapshot powerCreep, int gameTime, RoomObjectSnapshot controller, IEnumerable<RoomObjectSnapshot> additionalObjects, IReadOnlyDictionary<string, IReadOnlyList<IntentRecord>> objectIntents)
    {
        var objects = new Dictionary<string, RoomObjectSnapshot>(StringComparer.Ordinal)
        {
            [powerCreep.Id] = powerCreep,
            [controller.Id] = controller
        };

        foreach (var obj in additionalObjects) {
            objects[obj.Id] = obj;
        }

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
            new NullCreepStatsSink(),
            new NullGlobalMutationWriter(),
            new NullNotificationSink());
    }

    private static RoomObjectSnapshot CreateExtension(string id, int x, int y)
        => new(
            id,
            RoomObjectTypes.Extension,
            "W1N1",
            null,
            "user1",
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
            StructureType: null,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: 50,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: []);

    private static RoomObjectSnapshot CreateRampart(string id, int x, int y)
        => new(
            id,
            RoomObjectTypes.Rampart,
            "W1N1",
            null,
            "user1",
            x,
            y,
            Hits: 1000,
            HitsMax: 300000,
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
            Body: []);

    private static IReadOnlyDictionary<string, IReadOnlyList<IntentRecord>> CreatePowerIntent(PowerTypes power, string targetId)
    {
        var intent = new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal)
        {
            ["pc1"] =
            [
                new(
                    IntentKeys.UsePower,
                    [
                        new(
                            new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
                            {
                                [PowerCreepIntentFields.Power] = new(IntentFieldValueKind.Number, NumberValue: (int)power),
                                [PowerCreepIntentFields.Id] = new(IntentFieldValueKind.Text, TextValue: targetId)
                            })
                    ])
            ]
        };
        return intent;
    }

    private static RoomObjectSnapshot CreateTower(string id, int x, int y)
        => new(
            id,
            RoomObjectTypes.Tower,
            "W1N1",
            null,
            "user1",
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
            StructureType: null,
            Store: new Dictionary<string, int>(StringComparer.Ordinal) { [ResourceTypes.Energy] = 1000 },
            StoreCapacity: 1000,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: []);

    private static RoomObjectSnapshot CreateStorage(string id, int x, int y)
        => new(
            id,
            RoomObjectTypes.Storage,
            "W1N1",
            null,
            "user1",
            x,
            y,
            Hits: 10000,
            HitsMax: 10000,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: null,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: 1000000,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: []);

    private static RoomObjectSnapshot CreateLab(string id, int x, int y)
        => new(
            id,
            RoomObjectTypes.Lab,
            "W1N1",
            null,
            "user1",
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
            StructureType: null,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: 5000,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: []);

    private static RoomObjectSnapshot CreateObserver(string id, int x, int y)
        => new(
            id,
            RoomObjectTypes.Observer,
            "W1N1",
            null,
            "user1",
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
            StructureType: null,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: []);

    private static RoomObjectSnapshot CreateTerminal(string id, int x, int y)
        => new(
            id,
            RoomObjectTypes.Terminal,
            "W1N1",
            null,
            "user1",
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
            StructureType: null,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: 300000,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: []);

    private static RoomObjectSnapshot CreatePowerSpawn(string id, int x, int y)
        => new(
            id,
            RoomObjectTypes.PowerSpawn,
            "W1N1",
            null,
            "user1",
            x,
            y,
            Hits: 5000,
            HitsMax: 5000,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: null,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: 5000,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: []);

    private static RoomObjectSnapshot CreateSource(string id, int x, int y)
        => new(
            id,
            RoomObjectTypes.Source,
            "W1N1",
            null,
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
            Energy: 3000);

    private static RoomObjectSnapshot CreateMineral(string id, int x, int y, int mineralAmount)
        => new(
            id,
            RoomObjectTypes.Mineral,
            "W1N1",
            null,
            null,
            x,
            y,
            Hits: null,
            HitsMax: null,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: null,
            Density: 3,
            MineralType: ResourceTypes.Hydrogen,
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
            MineralAmount: mineralAmount);

    private static RoomObjectSnapshot CreateWall(string id, int x, int y)
        => new(
            id,
            RoomObjectTypes.ConstructedWall,
            "W1N1",
            null,
            "user1",
            x,
            y,
            Hits: 1000000,
            HitsMax: 300000000,
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
            Body: []);

    private static RoomObjectSnapshot CreateFactory(string id, int x, int y)
        => new(
            id,
            RoomObjectTypes.Factory,
            "W1N1",
            null,
            "user1",
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
            StructureType: null,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: 50000,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: []);

    private sealed class RecordingMutationWriter : IRoomMutationWriter
    {
        public List<(string ObjectId, RoomObjectPatchPayload Payload)> Patches { get; } = [];
        public List<RoomObjectSnapshot> Upserts { get; } = [];

        public void Upsert(RoomObjectSnapshot document)
            => Upserts.Add(document);

        public void Patch(string objectId, RoomObjectPatchPayload patch)
            => Patches.Add((objectId, patch));

        public void Remove(string objectId) { }

        public void SetRoomInfoPatch(RoomInfoPatchPayload patch) { }

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
            => Patches.Clear();
    }
}
