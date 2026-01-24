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

        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == terminal.Id && p.Payload.Store is not null);
        Assert.Equal(30, Payload.Store![ResourceTypes.Energy]);
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

        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == terminal.Id && p.Payload.Store is not null);
        Assert.Equal(10, Payload.Store![ResourceTypes.Energy]);
    }

    [Fact]
    public async Task Transfer_ExceedsTargetCapacity_ClampsToCapacity()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1", 100, store: new Dictionary<string, int> { [ResourceTypes.Energy] = 100 });
        var terminal = CreateTerminal("terminal1", 11, 10, "user1", store: new Dictionary<string, int> { [ResourceTypes.Energy] = 299_950 }, capacity: 300_000);
        var context = CreateContext([creep, terminal], CreateTransferIntent("user1", creep.Id, terminal.Id, ResourceTypes.Energy, 100));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (_, creepPayload) = writer.Patches.Single(p => p.ObjectId == creep.Id && p.Payload.Store is not null);
        Assert.Equal(50, creepPayload.Store![ResourceTypes.Energy]);

        var (_, terminalPayload) = writer.Patches.Single(p => p.ObjectId == terminal.Id && p.Payload.Store is not null);
        Assert.Equal(300_000, terminalPayload.Store![ResourceTypes.Energy]);
    }

    [Fact]
    public async Task Transfer_MultipleInSameTick_AccumulatesInLedger()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1", 100, store: new Dictionary<string, int> { [ResourceTypes.Energy] = 100 });
        var terminal = CreateTerminal("terminal1", 11, 10, "user1", store: [], capacity: 300_000);

        var argument1 = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: terminal.Id),
            [IntentKeys.ResourceType] = new(IntentFieldValueKind.Text, TextValue: ResourceTypes.Energy),
            [IntentKeys.Amount] = new(IntentFieldValueKind.Number, NumberValue: 30)
        });
        var argument2 = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: terminal.Id),
            [IntentKeys.ResourceType] = new(IntentFieldValueKind.Text, TextValue: ResourceTypes.Energy),
            [IntentKeys.Amount] = new(IntentFieldValueKind.Number, NumberValue: 40)
        });

        var record1 = new IntentRecord(IntentKeys.Transfer, [argument1]);
        var record2 = new IntentRecord(IntentKeys.Transfer, [argument2]);
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
        var context = CreateContext([creep, terminal], intents);
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (_, creepPayload) = writer.Patches.Single(p => p.ObjectId == creep.Id && p.Payload.Store is not null);
        Assert.Equal(30, creepPayload.Store![ResourceTypes.Energy]);

        var (_, terminalPayload) = writer.Patches.Single(p => p.ObjectId == terminal.Id && p.Payload.Store is not null);
        Assert.Equal(70, terminalPayload.Store![ResourceTypes.Energy]);
    }

    [Fact]
    public async Task Transfer_ToAnotherCreep_Works()
    {
        var creep1 = CreateCreep("creep1", 10, 10, "user1", 100, store: new Dictionary<string, int> { [ResourceTypes.Energy] = 50 });
        var creep2 = CreateCreep("creep2", 11, 10, "user1", 100, store: []);
        var context = CreateContext([creep1, creep2], CreateTransferIntent("user1", creep1.Id, creep2.Id, ResourceTypes.Energy, 30));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (_, creep1Payload) = writer.Patches.Single(p => p.ObjectId == creep1.Id && p.Payload.Store is not null);
        Assert.Equal(20, creep1Payload.Store![ResourceTypes.Energy]);

        var (_, creep2Payload) = writer.Patches.Single(p => p.ObjectId == creep2.Id && p.Payload.Store is not null);
        Assert.Equal(30, creep2Payload.Store![ResourceTypes.Energy]);
    }

    [Fact]
    public async Task Transfer_TargetAtFullCapacity_Fails()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1", 100, store: new Dictionary<string, int> { [ResourceTypes.Energy] = 50 });
        var terminal = CreateTerminal("terminal1", 11, 10, "user1", store: new Dictionary<string, int> { [ResourceTypes.Energy] = 300_000 }, capacity: 300_000);
        var context = CreateContext([creep, terminal], CreateTransferIntent("user1", creep.Id, terminal.Id, ResourceTypes.Energy, 30));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task Transfer_LabMineralFirstTime_SetsCapacity()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1", 100, store: new Dictionary<string, int> { [ResourceTypes.Hydrogen] = 50 });
        var lab = CreateLab("lab1", 11, 10, "user1");
        var context = CreateContext([creep, lab], CreateTransferIntent("user1", creep.Id, lab.Id, ResourceTypes.Hydrogen, 30));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (_, creepPayload) = writer.Patches.Single(p => p.ObjectId == creep.Id && p.Payload.Store is not null);
        Assert.Equal(20, creepPayload.Store![ResourceTypes.Hydrogen]);

        var labStorePatches = writer.Patches.Where(p => p.ObjectId == lab.Id && p.Payload.Store is not null).ToList();
        Assert.Single(labStorePatches);
        var (_, labStorePayload) = labStorePatches[0];
        Assert.Equal(30, labStorePayload.Store![ResourceTypes.Hydrogen]);

        var labCapacityPatches = writer.Patches.Where(p => p.ObjectId == lab.Id && p.Payload.StoreCapacityResource is not null).ToList();
        Assert.Single(labCapacityPatches);
        var (_, labCapacityPayload) = labCapacityPatches[0];
        Assert.Equal(ScreepsGameConstants.LabMineralCapacity, labCapacityPayload.StoreCapacityResource![ResourceTypes.Hydrogen]);
    }

    [Fact]
    public async Task Transfer_LabEnergy_DoesNotSetCapacity()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1", 100, store: new Dictionary<string, int> { [ResourceTypes.Energy] = 50 });
        var lab = CreateLab("lab1", 11, 10, "user1");
        var context = CreateContext([creep, lab], CreateTransferIntent("user1", creep.Id, lab.Id, ResourceTypes.Energy, 30));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (_, creepPayload) = writer.Patches.Single(p => p.ObjectId == creep.Id && p.Payload.Store is not null);
        Assert.Equal(20, creepPayload.Store![ResourceTypes.Energy]);

        var (_, labPayload) = writer.Patches.Single(p => p.ObjectId == lab.Id && p.Payload.Store is not null);
        Assert.Equal(30, labPayload.Store![ResourceTypes.Energy]);

        Assert.DoesNotContain(writer.Patches, p => p.ObjectId == lab.Id && p.Payload.StoreCapacityResource is not null);
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

        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == terminal.Id && p.Payload.Store is not null);
        Assert.Equal(70, Payload.Store![ResourceTypes.Energy]);
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
    public async Task Withdraw_ExceedsAvailable_ClampsToAvailable()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1", 100, store: []);
        var terminal = CreateTerminal("terminal1", 11, 10, "user1", store: new Dictionary<string, int> { [ResourceTypes.Energy] = 20 }, capacity: 300_000);
        var context = CreateContext([creep, terminal], CreateWithdrawIntent("user1", creep.Id, terminal.Id, ResourceTypes.Energy, 50));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (_, creepPayload) = writer.Patches.Single(p => p.ObjectId == creep.Id && p.Payload.Store is not null);
        Assert.Equal(20, creepPayload.Store![ResourceTypes.Energy]);

        var (_, terminalPayload) = writer.Patches.Single(p => p.ObjectId == terminal.Id && p.Payload.Store is not null);
        Assert.Equal(0, terminalPayload.Store![ResourceTypes.Energy]);
    }

    [Fact]
    public async Task Withdraw_ExceedsCreepCapacity_ClampsToCapacity()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1", 30, store: []);
        var terminal = CreateTerminal("terminal1", 11, 10, "user1", store: new Dictionary<string, int> { [ResourceTypes.Energy] = 100 }, capacity: 300_000);
        var context = CreateContext([creep, terminal], CreateWithdrawIntent("user1", creep.Id, terminal.Id, ResourceTypes.Energy, 50));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (_, creepPayload) = writer.Patches.Single(p => p.ObjectId == creep.Id && p.Payload.Store is not null);
        Assert.Equal(30, creepPayload.Store![ResourceTypes.Energy]);

        var (_, terminalPayload) = writer.Patches.Single(p => p.ObjectId == terminal.Id && p.Payload.Store is not null);
        Assert.Equal(70, terminalPayload.Store![ResourceTypes.Energy]);
    }

    [Fact]
    public async Task Withdraw_FromNuker_Fails()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1", 100, store: []);
        var nuker = CreateNuker("nuker1", 11, 10, "user1", store: new Dictionary<string, int> { [ResourceTypes.Energy] = 100 });
        var context = CreateContext([creep, nuker], CreateWithdrawIntent("user1", creep.Id, nuker.Id, ResourceTypes.Energy, 30));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task Withdraw_TerminalDisruption_BlocksWithdraw()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1", 100, store: []);
        var terminal = CreateTerminalWithEffect("terminal1", 11, 10, "user1", store: new Dictionary<string, int> { [ResourceTypes.Energy] = 100 }, capacity: 300_000, effectPower: PowerTypes.DisruptTerminal, effectEndTime: 200);
        var context = CreateContext([creep, terminal], CreateWithdrawIntent("user1", creep.Id, terminal.Id, ResourceTypes.Energy, 30), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task Withdraw_LabMineralLastWithdraw_ClearsCapacity()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1", 100, store: []);
        var lab = CreateLab("lab1", 11, 10, "user1", store: new Dictionary<string, int> { [ResourceTypes.Hydrogen] = 30 }, storeCapacityResource: new Dictionary<string, int> { [ResourceTypes.Hydrogen] = ScreepsGameConstants.LabMineralCapacity });
        var context = CreateContext([creep, lab], CreateWithdrawIntent("user1", creep.Id, lab.Id, ResourceTypes.Hydrogen, 30));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (_, creepPayload) = writer.Patches.Single(p => p.ObjectId == creep.Id && p.Payload.Store is not null);
        Assert.Equal(30, creepPayload.Store![ResourceTypes.Hydrogen]);

        var labStorePatches = writer.Patches.Where(p => p.ObjectId == lab.Id && p.Payload.Store is not null).ToList();
        Assert.Single(labStorePatches);
        var (_, labStorePayload) = labStorePatches[0];
        Assert.Equal(0, labStorePayload.Store![ResourceTypes.Hydrogen]);

        var labCapacityPatches = writer.Patches.Where(p => p.ObjectId == lab.Id && p.Payload.StoreCapacityResource is not null).ToList();
        Assert.Single(labCapacityPatches);
        var (_, labCapacityPayload) = labCapacityPatches[0];
        Assert.Empty(labCapacityPayload.StoreCapacityResource!);
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
    public async Task Pickup_CannotPickupFromTombstone_Fails()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1", 100, store: []);
        var tombstone = CreateTombstone("tomb1", 11, 10, store: new Dictionary<string, int> { [ResourceTypes.Energy] = 50 });
        var context = CreateContext([creep, tombstone], CreatePickupIntent("user1", creep.Id, tombstone.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task Pickup_NotAdjacent_Fails()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1", 100, store: []);
        var drop = CreateDrop("drop1", 15, 15, ResourceTypes.Energy, 50);
        var context = CreateContext([creep, drop], CreatePickupIntent("user1", creep.Id, drop.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task Pickup_NonExistentDrop_Fails()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1", 100, store: []);
        var context = CreateContext([creep], CreatePickupIntent("user1", creep.Id, "nonexistent"));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task Pickup_CreepAtFullCapacity_Fails()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1", 30, store: new Dictionary<string, int> { [ResourceTypes.Energy] = 30 });
        var drop = CreateDrop("drop1", 11, 10, ResourceTypes.Energy, 50);
        var context = CreateContext([creep, drop], CreatePickupIntent("user1", creep.Id, drop.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(writer.Patches);
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

        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == container.Id && p.Payload.Store is not null);
        Assert.Equal(30, Payload.Store![ResourceTypes.Energy]);

        Assert.Empty(writer.Upserts);
    }

    [Fact]
    public async Task Drop_ContainerFull_SpillsToGround()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1", 100, store: new Dictionary<string, int> { [ResourceTypes.Energy] = 100 });
        var container = CreateContainer("container1", 10, 10, store: new Dictionary<string, int> { [ResourceTypes.Energy] = 1980 }, capacity: 2000);
        var context = CreateContext([creep, container], CreateDropIntent("user1", creep.Id, ResourceTypes.Energy, 50));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (_, creepPayload) = writer.Patches.Single(p => p.ObjectId == creep.Id && p.Payload.Store is not null);
        Assert.Equal(50, creepPayload.Store![ResourceTypes.Energy]);

        var (_, containerPayload) = writer.Patches.Single(p => p.ObjectId == container.Id && p.Payload.Store is not null);
        Assert.Equal(2000, containerPayload.Store![ResourceTypes.Energy]);

        var groundDrop = writer.Upserts.Single();
        Assert.Equal(RoomObjectTypes.Resource, groundDrop.Type);
        Assert.Equal(ResourceTypes.Energy, groundDrop.ResourceType);
        Assert.Equal(30, groundDrop.ResourceAmount);
    }

    [Fact]
    public async Task Drop_StacksWithExistingDrop_MergesAmount()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1", 100, store: new Dictionary<string, int> { [ResourceTypes.Energy] = 50 });
        var existingDrop = CreateDrop("drop1", 10, 10, ResourceTypes.Energy, 20);
        var context = CreateContext([creep, existingDrop], CreateDropIntent("user1", creep.Id, ResourceTypes.Energy, 30));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (_, creepPayload) = writer.Patches.Single(p => p.ObjectId == creep.Id && p.Payload.Store is not null);
        Assert.Equal(20, creepPayload.Store![ResourceTypes.Energy]);

        var updatedDrop = writer.Upserts.Single();
        Assert.Equal(existingDrop.Id, updatedDrop.Id);
        Assert.Equal(50, updatedDrop.ResourceAmount);
    }

    [Fact]
    public async Task Drop_ExceedsAvailable_ClampsToAvailable()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1", 100, store: new Dictionary<string, int> { [ResourceTypes.Energy] = 20 });
        var context = CreateContext([creep], CreateDropIntent("user1", creep.Id, ResourceTypes.Energy, 50));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (_, creepPayload) = writer.Patches.Single(p => p.ObjectId == creep.Id && p.Payload.Store is not null);
        Assert.Equal(0, creepPayload.Store![ResourceTypes.Energy]);

        var drop = writer.Upserts.Single();
        Assert.Equal(20, drop.ResourceAmount);
    }

    [Fact]
    public async Task Drop_MultipleInSameTick_AccumulatesInLedger()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1", 100, store: new Dictionary<string, int> { [ResourceTypes.Energy] = 100 });

        var argument1 = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.ResourceType] = new(IntentFieldValueKind.Text, TextValue: ResourceTypes.Energy),
            [IntentKeys.Amount] = new(IntentFieldValueKind.Number, NumberValue: 30)
        });
        var argument2 = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.ResourceType] = new(IntentFieldValueKind.Text, TextValue: ResourceTypes.Energy),
            [IntentKeys.Amount] = new(IntentFieldValueKind.Number, NumberValue: 40)
        });

        var record1 = new IntentRecord(IntentKeys.Drop, [argument1]);
        var record2 = new IntentRecord(IntentKeys.Drop, [argument2]);
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
        var context = CreateContext([creep], intents);
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (_, creepPayload) = writer.Patches.Single(p => p.ObjectId == creep.Id && p.Payload.Store is not null);
        Assert.Equal(30, creepPayload.Store![ResourceTypes.Energy]);

        Assert.Equal(2, writer.Upserts.Count);
        var dropId = writer.Upserts[0].Id;
        Assert.Equal(dropId, writer.Upserts[1].Id);
        Assert.Equal(30, writer.Upserts[0].ResourceAmount);
        Assert.Equal(70, writer.Upserts[1].ResourceAmount);
        Assert.Equal(creep.X, writer.Upserts[1].X);
        Assert.Equal(creep.Y, writer.Upserts[1].Y);
    }

    [Fact]
    public async Task Integration_RoundTrip_PickupTransferDrop()
    {
        var creep1 = CreateCreep("creep1", 10, 10, "user1", 100, store: []);
        var creep2 = CreateCreep("creep2", 11, 10, "user1", 100, store: []);
        var drop = CreateDrop("drop1", 11, 11, ResourceTypes.Energy, 50);

        var pickupArg = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: drop.Id)
        });
        var transferArg = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: creep2.Id),
            [IntentKeys.ResourceType] = new(IntentFieldValueKind.Text, TextValue: ResourceTypes.Energy),
            [IntentKeys.Amount] = new(IntentFieldValueKind.Number, NumberValue: 30)
        });
        var dropArg = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.ResourceType] = new(IntentFieldValueKind.Text, TextValue: ResourceTypes.Energy),
            [IntentKeys.Amount] = new(IntentFieldValueKind.Number, NumberValue: 10)
        });

        var creep1Intents = new IntentRecord[]
        {
            new(IntentKeys.Pickup, [pickupArg]),
            new(IntentKeys.Transfer, [transferArg])
        };
        var creep2Intents = new IntentRecord[]
        {
            new(IntentKeys.Drop, [dropArg])
        };

        var objectIntents = new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal)
        {
            [creep1.Id] = creep1Intents,
            [creep2.Id] = creep2Intents
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
        var context = CreateContext([creep1, creep2, drop], intents);
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var creep1Patches = writer.Patches.Where(p => p.ObjectId == creep1.Id && p.Payload.Store is not null).ToList();
        Assert.Single(creep1Patches);
        Assert.Equal(20, creep1Patches[0].Payload.Store![ResourceTypes.Energy]);

        var creep2Patches = writer.Patches.Where(p => p.ObjectId == creep2.Id && p.Payload.Store is not null).ToList();
        Assert.Single(creep2Patches);
        Assert.Equal(20, creep2Patches[0].Payload.Store![ResourceTypes.Energy]);

        Assert.Contains(drop.Id, writer.Removals);
    }

    [Fact]
    public async Task Integration_LabWorkflow_TransferAndWithdrawWithCapacityTracking()
    {
        var creep = CreateCreep("creep1", 10, 10, "user1", 100, store: new Dictionary<string, int> { [ResourceTypes.Hydrogen] = 50 });
        var lab = CreateLab("lab1", 11, 10, "user1");

        var transferArg = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: lab.Id),
            [IntentKeys.ResourceType] = new(IntentFieldValueKind.Text, TextValue: ResourceTypes.Hydrogen),
            [IntentKeys.Amount] = new(IntentFieldValueKind.Number, NumberValue: 50)
        });

        var transferIntent = new IntentRecord(IntentKeys.Transfer, [transferArg]);
        var objectIntents = new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal)
        {
            [creep.Id] = [transferIntent]
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

        var transferIntents = new RoomIntentSnapshot("W1N1", "shard0", users);
        var context1 = CreateContext([creep, lab], transferIntents);
        var writer1 = (FakeMutationWriter)context1.MutationWriter;

        await _step.ExecuteAsync(context1, TestContext.Current.CancellationToken);

        var labCapacityPatches = writer1.Patches.Where(p => p.ObjectId == lab.Id && p.Payload.StoreCapacityResource is not null).ToList();
        Assert.Single(labCapacityPatches);
        Assert.Equal(ScreepsGameConstants.LabMineralCapacity, labCapacityPatches[0].Payload.StoreCapacityResource![ResourceTypes.Hydrogen]);

        var updatedLab = CreateLab("lab1", 11, 10, "user1", store: new Dictionary<string, int> { [ResourceTypes.Hydrogen] = 50 }, storeCapacityResource: new Dictionary<string, int> { [ResourceTypes.Hydrogen] = ScreepsGameConstants.LabMineralCapacity });
        var updatedCreep = CreateCreep("creep1", 10, 10, "user1", 100, store: []);

        var withdrawArg = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: updatedLab.Id),
            [IntentKeys.ResourceType] = new(IntentFieldValueKind.Text, TextValue: ResourceTypes.Hydrogen),
            [IntentKeys.Amount] = new(IntentFieldValueKind.Number, NumberValue: 50)
        });

        var withdrawIntent = new IntentRecord(IntentKeys.Withdraw, [withdrawArg]);
        var objectIntents2 = new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal)
        {
            [updatedCreep.Id] = [withdrawIntent]
        };

        var envelope2 = new IntentEnvelope(
            "user1",
            objectIntents2,
            new Dictionary<string, SpawnIntentEnvelope>(StringComparer.Ordinal),
            new Dictionary<string, CreepIntentEnvelope>(StringComparer.Ordinal));

        var users2 = new Dictionary<string, IntentEnvelope>(StringComparer.Ordinal)
        {
            ["user1"] = envelope2
        };

        var withdrawIntents = new RoomIntentSnapshot("W1N1", "shard0", users2);
        var context2 = CreateContext([updatedCreep, updatedLab], withdrawIntents);
        var writer2 = (FakeMutationWriter)context2.MutationWriter;

        await _step.ExecuteAsync(context2, TestContext.Current.CancellationToken);

        var labCapacityPatches2 = writer2.Patches.Where(p => p.ObjectId == updatedLab.Id && p.Payload.StoreCapacityResource is not null).ToList();
        Assert.Single(labCapacityPatches2);
        Assert.Empty(labCapacityPatches2[0].Payload.StoreCapacityResource!);

        var creepPatches2 = writer2.Patches.Where(p => p.ObjectId == updatedCreep.Id && p.Payload.Store is not null).ToList();
        Assert.Single(creepPatches2);
        Assert.Equal(50, creepPatches2[0].Payload.Store![ResourceTypes.Hydrogen]);
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

        return new RoomProcessorContext(state, new FakeMutationWriter(), new NullCreepStatsSink(), new NullGlobalMutationWriter());
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

    private static RoomObjectSnapshot CreateNuker(string id, int x, int y, string userId, Dictionary<string, int> store)
        => new(
            id,
            RoomObjectTypes.Nuker,
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
            StructureType: RoomObjectTypes.Nuker,
            Store: store,
            StoreCapacity: 300_000,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: []);

    private static RoomObjectSnapshot CreateTerminalWithEffect(string id, int x, int y, string userId, Dictionary<string, int> store, int capacity, PowerTypes effectPower, int effectEndTime)
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
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>
            {
                [effectPower] = new(Power: effectPower, Level: 1, EndTime: effectEndTime)
            },
            Spawning: null,
            Body: []);

    private static RoomObjectSnapshot CreateTombstone(string id, int x, int y, Dictionary<string, int> store)
        => new(
            id,
            RoomObjectTypes.Tombstone,
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
            Store: store,
            StoreCapacity: 2000,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: [],
            DeathTime: 100);

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

#pragma warning disable CA1822 // Mark members as static
        public int GetMutationCount() => 0;
#pragma warning restore CA1822

        public Task FlushAsync(CancellationToken token = default) => Task.CompletedTask;

#pragma warning disable CA1822 // Method cannot be static as it implements interface member
        public bool TryGetPendingPatch(string objectId, out RoomObjectPatchPayload patch) { patch = new RoomObjectPatchPayload(); return false; }

        public void Reset()
        {
            Patches.Clear();
            Upserts.Clear();
            Removals.Clear();
        }
    }
}
