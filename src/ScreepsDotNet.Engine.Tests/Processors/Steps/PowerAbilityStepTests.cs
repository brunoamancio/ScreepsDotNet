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
        var spawnPatch = writer.Patches.FirstOrDefault(p => p.ObjectId == "spawn1");
        Assert.NotNull(spawnPatch.Payload.Effects);
        Assert.True(spawnPatch.Payload.Effects.ContainsKey(PowerTypes.OperateSpawn));
        Assert.Equal(3, spawnPatch.Payload.Effects[PowerTypes.OperateSpawn].Level);
        Assert.Equal(1100, spawnPatch.Payload.Effects[PowerTypes.OperateSpawn].EndTime);
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
        var towerPatch = writer.Patches.FirstOrDefault(p => p.ObjectId == "tower1");
        Assert.True(towerPatch.ObjectId is null || towerPatch.Payload.Effects is null);
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
        var towerPatch = writer.Patches.FirstOrDefault(p => p.ObjectId == "tower1");
        Assert.NotNull(towerPatch.Payload.Effects);
        Assert.True(towerPatch.Payload.Effects.ContainsKey(PowerTypes.OperateTower));
        Assert.Equal(2, towerPatch.Payload.Effects[PowerTypes.OperateTower].Level);
        Assert.Equal(200, towerPatch.Payload.Effects[PowerTypes.OperateTower].EndTime);
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
        var spawnPatch = writer.Patches.FirstOrDefault(p => p.ObjectId == "spawn1");
        Assert.True(spawnPatch.ObjectId is null || spawnPatch.Payload.Effects is null);
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
        var storagePatch = writer.Patches.FirstOrDefault(p => p.ObjectId == "storage1");
        Assert.NotNull(storagePatch.Payload.Effects);
        Assert.True(storagePatch.Payload.Effects.ContainsKey(PowerTypes.OperateStorage));
        Assert.Equal(1, storagePatch.Payload.Effects[PowerTypes.OperateStorage].Level);
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
        var targetPatch = writer.Patches.FirstOrDefault(p => p.ObjectId == "spawn1");
        Assert.True(targetPatch.ObjectId is null || targetPatch.Payload.Effects is null || targetPatch.Payload.Effects.Count == 0);
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
        var labPatch = writer.Patches.FirstOrDefault(p => p.ObjectId == "lab1");
        Assert.NotNull(labPatch.Payload.Effects);
        Assert.True(labPatch.Payload.Effects.ContainsKey(PowerTypes.OperateLab));
        Assert.Equal(4, labPatch.Payload.Effects[PowerTypes.OperateLab].Level);
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
        var targetPatch = writer.Patches.FirstOrDefault(p => p.ObjectId == "spawn1");
        Assert.True(targetPatch.ObjectId is null || targetPatch.Payload.Effects is null || targetPatch.Payload.Effects.Count == 0);
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
        var observerPatch = writer.Patches.FirstOrDefault(p => p.ObjectId == "observer1");
        Assert.NotNull(observerPatch.Payload.Effects);
        Assert.True(observerPatch.Payload.Effects.ContainsKey(PowerTypes.OperateObserver));
        Assert.Equal(5, observerPatch.Payload.Effects[PowerTypes.OperateObserver].Level);
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
        var targetPatch = writer.Patches.FirstOrDefault(p => p.ObjectId == "spawn1");
        Assert.True(targetPatch.ObjectId is null || targetPatch.Payload.Effects is null || targetPatch.Payload.Effects.Count == 0);
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
        var terminalPatch = writer.Patches.FirstOrDefault(p => p.ObjectId == "terminal1");
        Assert.NotNull(terminalPatch.Payload.Effects);
        Assert.True(terminalPatch.Payload.Effects.ContainsKey(PowerTypes.OperateTerminal));
        Assert.Equal(2, terminalPatch.Payload.Effects[PowerTypes.OperateTerminal].Level);
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
        var targetPatch = writer.Patches.FirstOrDefault(p => p.ObjectId == "spawn1");
        Assert.True(targetPatch.ObjectId is null || targetPatch.Payload.Effects is null || targetPatch.Payload.Effects.Count == 0);
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
        var powerSpawnPatch = writer.Patches.FirstOrDefault(p => p.ObjectId == "powerSpawn1");
        Assert.NotNull(powerSpawnPatch.Payload.Effects);
        Assert.True(powerSpawnPatch.Payload.Effects.ContainsKey(PowerTypes.OperatePower));
        Assert.Equal(3, powerSpawnPatch.Payload.Effects[PowerTypes.OperatePower].Level);
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
        var targetPatch = writer.Patches.FirstOrDefault(p => p.ObjectId == "spawn1");
        Assert.True(targetPatch.ObjectId is null || targetPatch.Payload.Effects is null || targetPatch.Payload.Effects.Count == 0);
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
        var controllerPatch = writer.Patches.FirstOrDefault(p => p.ObjectId == "controller1");
        Assert.NotNull(controllerPatch.Payload.Effects);
        Assert.True(controllerPatch.Payload.Effects.ContainsKey(PowerTypes.OperateController));
        Assert.Equal(4, controllerPatch.Payload.Effects[PowerTypes.OperateController].Level);
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
        var targetPatch = writer.Patches.FirstOrDefault(p => p.ObjectId == "spawn1");
        Assert.True(targetPatch.ObjectId is null || targetPatch.Payload.Effects is null || targetPatch.Payload.Effects.Count == 0);
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
        var spawnPatch = writer.Patches.FirstOrDefault(p => p.ObjectId == "spawn1");
        Assert.NotNull(spawnPatch.Payload.Effects);
        Assert.True(spawnPatch.Payload.Effects.ContainsKey(PowerTypes.DisruptSpawn));
        Assert.Equal(3, spawnPatch.Payload.Effects[PowerTypes.DisruptSpawn].Level);
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
        var targetPatch = writer.Patches.FirstOrDefault(p => p.ObjectId == "tower1");
        Assert.True(targetPatch.ObjectId is null || targetPatch.Payload.Effects is null || targetPatch.Payload.Effects.Count == 0);
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
        var towerPatch = writer.Patches.FirstOrDefault(p => p.ObjectId == "tower1");
        Assert.NotNull(towerPatch.Payload.Effects);
        Assert.True(towerPatch.Payload.Effects.ContainsKey(PowerTypes.DisruptTower));
        Assert.Equal(2, towerPatch.Payload.Effects[PowerTypes.DisruptTower].Level);
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
        var targetPatch = writer.Patches.FirstOrDefault(p => p.ObjectId == "spawn1");
        Assert.True(targetPatch.ObjectId is null || targetPatch.Payload.Effects is null || targetPatch.Payload.Effects.Count == 0);
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
        var sourcePatch = writer.Patches.FirstOrDefault(p => p.ObjectId == "source1");
        Assert.NotNull(sourcePatch.Payload.Effects);
        Assert.True(sourcePatch.Payload.Effects.ContainsKey(PowerTypes.DisruptSource));
        Assert.Equal(4, sourcePatch.Payload.Effects[PowerTypes.DisruptSource].Level);
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
        var targetPatch = writer.Patches.FirstOrDefault(p => p.ObjectId == "spawn1");
        Assert.True(targetPatch.ObjectId is null || targetPatch.Payload.Effects is null || targetPatch.Payload.Effects.Count == 0);
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
        var terminalPatch = writer.Patches.FirstOrDefault(p => p.ObjectId == "terminal1");
        Assert.NotNull(terminalPatch.Payload.Effects);
        Assert.True(terminalPatch.Payload.Effects.ContainsKey(PowerTypes.DisruptTerminal));
        Assert.Equal(3, terminalPatch.Payload.Effects[PowerTypes.DisruptTerminal].Level);
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
        var targetPatch = writer.Patches.FirstOrDefault(p => p.ObjectId == "spawn1");
        Assert.True(targetPatch.ObjectId is null || targetPatch.Payload.Effects is null || targetPatch.Payload.Effects.Count == 0);
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
        var sourcePatch = writer.Patches.FirstOrDefault(p => p.ObjectId == "source1");
        Assert.NotNull(sourcePatch.Payload.Effects);
        Assert.True(sourcePatch.Payload.Effects.ContainsKey(PowerTypes.RegenSource));
        Assert.Equal(5, sourcePatch.Payload.Effects[PowerTypes.RegenSource].Level);
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
        var targetPatch = writer.Patches.FirstOrDefault(p => p.ObjectId == "spawn1");
        Assert.True(targetPatch.ObjectId is null || targetPatch.Payload.Effects is null || targetPatch.Payload.Effects.Count == 0);
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
        var mineralPatch = writer.Patches.FirstOrDefault(p => p.ObjectId == "mineral1");
        Assert.NotNull(mineralPatch.Payload.Effects);
        Assert.True(mineralPatch.Payload.Effects.ContainsKey(PowerTypes.RegenMineral));
        Assert.Equal(3, mineralPatch.Payload.Effects[PowerTypes.RegenMineral].Level);
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
        var targetPatch = writer.Patches.FirstOrDefault(p => p.ObjectId == "spawn1");
        Assert.True(targetPatch.ObjectId is null || targetPatch.Payload.Effects is null || targetPatch.Payload.Effects.Count == 0);
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
        var wallPatch = writer.Patches.FirstOrDefault(p => p.ObjectId == "wall1");
        Assert.NotNull(wallPatch.Payload.Effects);
        Assert.True(wallPatch.Payload.Effects.ContainsKey(PowerTypes.Fortify));
        Assert.Equal(4, wallPatch.Payload.Effects[PowerTypes.Fortify].Level);
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
        var targetPatch = writer.Patches.FirstOrDefault(p => p.ObjectId == "spawn1");
        Assert.True(targetPatch.ObjectId is null || targetPatch.Payload.Effects is null || targetPatch.Payload.Effects.Count == 0);
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
        var factoryPatch = writer.Patches.FirstOrDefault(p => p.ObjectId == "factory1");
        Assert.NotNull(factoryPatch.Payload.Effects);
        Assert.True(factoryPatch.Payload.Effects.ContainsKey(PowerTypes.OperateFactory));
        Assert.Equal(2, factoryPatch.Payload.Effects[PowerTypes.OperateFactory].Level);
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
        var targetPatch = writer.Patches.FirstOrDefault(p => p.ObjectId == "spawn1");
        Assert.True(targetPatch.ObjectId is null || targetPatch.Payload.Effects is null || targetPatch.Payload.Effects.Count == 0);
    }

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
