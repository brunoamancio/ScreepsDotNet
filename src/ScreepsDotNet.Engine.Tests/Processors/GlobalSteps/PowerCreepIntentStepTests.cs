namespace ScreepsDotNet.Engine.Tests.Processors.GlobalSteps;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.Models;
using ScreepsDotNet.Engine.Processors.GlobalSteps;

public sealed class PowerCreepIntentStepTests
{
    [Fact]
    public async Task ExecuteAsync_RenameIntentStagesMutation()
    {
        var powerCreep = new PowerCreepSnapshot(
            "64c5d5f7e4b07a2c1a000001",
            "user1",
            "old",
            "operator",
            10,
            5000,
            new Dictionary<string, int>(),
            100,
            0,
            null,
            null,
            new Dictionary<PowerTypes, PowerCreepPowerSnapshot>());

        var intents = new[]
        {
            new IntentRecord(
                GlobalIntentTypes.RenamePowerCreep,
                [
                    new IntentArgument(new Dictionary<string, IntentFieldValue>
                    {
                        [PowerCreepIntentFields.Id] = new(IntentFieldValueKind.Text, TextValue: powerCreep.Id),
                        [PowerCreepIntentFields.Name] = new(IntentFieldValueKind.Text, TextValue: "NewPowerCreep")
                    })
                ])
        };

        var userIntentSnapshot = new GlobalUserIntentSnapshot("intentDoc", "user1", intents);
        var state = CreateGlobalState(powerCreep, userIntentSnapshot);
        var writer = new RecordingGlobalMutationWriter();
        var context = new GlobalProcessorContext(state, writer);
        var step = new PowerCreepIntentStep();

        await step.ExecuteAsync(context, CancellationToken.None);

        var (Id, Patch) = Assert.Single(writer.PowerCreepPatches);
        Assert.Equal(powerCreep.Id, Id);
        Assert.Equal("NewPowerCreep", Patch.Name);
    }

    [Fact]
    public async Task ExecuteAsync_DeleteIntentSetsDeleteTime()
    {
        var powerCreep = CreatePowerCreep(spawnCooldownTime: 0);
        var intents = new[]
        {
            new IntentRecord(
                GlobalIntentTypes.DeletePowerCreep,
                [
                    new IntentArgument(new Dictionary<string, IntentFieldValue>
                    {
                        [PowerCreepIntentFields.Id] = new(IntentFieldValueKind.Text, TextValue: powerCreep.Id)
                    })
                ])
        };

        var timestamp = 1_000L;
        var writer = new RecordingGlobalMutationWriter();
        var context = new GlobalProcessorContext(
            CreateGlobalState(powerCreep, new GlobalUserIntentSnapshot("doc", "user1", intents), 0),
            writer);
        var step = new PowerCreepIntentStep(() => timestamp);

        await step.ExecuteAsync(context, CancellationToken.None);

        var (Id, Patch) = Assert.Single(writer.PowerCreepPatches);
        Assert.Equal(powerCreep.Id, Id);
        Assert.Equal(timestamp + ScreepsGameConstants.PowerCreepDeleteCooldownMilliseconds, Patch.DeleteTime);
        Assert.False(Patch.ClearDeleteTime);
    }

    [Fact]
    public async Task ExecuteAsync_DeleteIntentCancelsDeleteTime()
    {
        var powerCreep = CreatePowerCreep(spawnCooldownTime: 0, deleteTime: 5000);
        var intents = new[]
        {
            new IntentRecord(
                GlobalIntentTypes.DeletePowerCreep,
                [
                    new IntentArgument(new Dictionary<string, IntentFieldValue>
                    {
                        [PowerCreepIntentFields.Id] = new(IntentFieldValueKind.Text, TextValue: powerCreep.Id),
                        [PowerCreepIntentFields.Cancel] = new(IntentFieldValueKind.Boolean, BooleanValue: true)
                    })
                ])
        };

        var writer = new RecordingGlobalMutationWriter();
        var context = new GlobalProcessorContext(
            CreateGlobalState(powerCreep, new GlobalUserIntentSnapshot("doc", "user1", intents), 0),
            writer);
        var step = new PowerCreepIntentStep(() => 10);

        await step.ExecuteAsync(context, CancellationToken.None);

        var (Id, Patch) = Assert.Single(writer.PowerCreepPatches);
        Assert.True(Patch.ClearDeleteTime);
        Assert.Null(Patch.DeleteTime);
    }

    [Fact]
    public async Task ExecuteAsync_DeleteIntentDuringExperimentationRemoves()
    {
        var powerCreep = CreatePowerCreep(spawnCooldownTime: 0);
        var intents = new[]
        {
            new IntentRecord(
                GlobalIntentTypes.DeletePowerCreep,
                [
                    new IntentArgument(new Dictionary<string, IntentFieldValue>
                    {
                        [PowerCreepIntentFields.Id] = new(IntentFieldValueKind.Text, TextValue: powerCreep.Id)
                    })
                ])
        };

        var timestamp = 1_000L;
        var writer = new RecordingGlobalMutationWriter();
        var context = new GlobalProcessorContext(
            CreateGlobalState(powerCreep, new GlobalUserIntentSnapshot("doc", "user1", intents), 2_000),
            writer);
        var step = new PowerCreepIntentStep(() => timestamp);

        await step.ExecuteAsync(context, CancellationToken.None);

        var removed = Assert.Single(writer.RemovedPowerCreeps);
        Assert.Equal(powerCreep.Id, removed);
    }

    private static PowerCreepSnapshot CreatePowerCreep(long? spawnCooldownTime = 0, long? deleteTime = null)
        => new(
            "64c5d5f7e4b07a2c1a000001",
            "user1",
            "old",
            "operator",
            10,
            5000,
            new Dictionary<string, int>(),
            100,
            spawnCooldownTime,
            deleteTime,
            null,
            new Dictionary<PowerTypes, PowerCreepPowerSnapshot>());

    private static GlobalState CreateGlobalState(
        PowerCreepSnapshot powerCreep,
        GlobalUserIntentSnapshot intentSnapshot,
        double powerExperimentationTime = 0)
    {
        var market = new GlobalMarketSnapshot(
            [],
            new Dictionary<string, UserState>
            {
                ["user1"] = new("user1", "player", 0, 0, 0, true, powerExperimentationTime, new Dictionary<string, int>())
            },
            [powerCreep],
            [intentSnapshot],
            "shard0");

        return new GlobalState(
            12345,
            [],
            new Dictionary<string, RoomInfoSnapshot>(),
            new Dictionary<string, RoomExitTopology>(),
            [],
            market);
    }
}
