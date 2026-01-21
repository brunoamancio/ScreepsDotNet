namespace ScreepsDotNet.Engine.Tests.Processors.Steps;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.Bulk;
using ScreepsDotNet.Engine.Data.Models;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.Helpers;
using ScreepsDotNet.Engine.Processors.Steps;

public sealed class LabIntentStepTests
{
    private readonly LabIntentStep _step = new();

    #region runReaction Tests

    [Fact]
    public async Task RunReaction_BasicReaction_ProducesCompound()
    {
        var lab = CreateLab("lab1", 10, 10, "user1", new Dictionary<string, int>(StringComparer.Ordinal));
        var lab1 = CreateLab("lab2", 11, 11, "user1", new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [ResourceTypes.Hydrogen] = 100
        });
        var lab2 = CreateLab("lab3", 12, 12, "user1", new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [ResourceTypes.Oxygen] = 100
        });
        var context = CreateContext([lab, lab1, lab2],
            CreateRunReactionIntent("user1", lab.Id, lab1.Id, lab2.Id), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == lab.Id);
        Assert.Equal(5, Payload.Store![ResourceTypes.Hydroxide]);
        Assert.Equal(100 + LabReactions.CooldownTimes[ResourceTypes.Hydroxide], Payload.CooldownTime);
        Assert.Equal(ScreepsGameConstants.LabMineralCapacity, Payload.StoreCapacityResource![ResourceTypes.Hydroxide]);

        var lab1Patch = writer.Patches.Single(p => p.ObjectId == lab1.Id);
        Assert.Equal(95, lab1Patch.Payload.Store![ResourceTypes.Hydrogen]);

        var lab2Patch = writer.Patches.Single(p => p.ObjectId == lab2.Id);
        Assert.Equal(95, lab2Patch.Payload.Store![ResourceTypes.Oxygen]);
    }

    [Fact]
    public async Task RunReaction_ComplexReaction_ProducesT3Compound()
    {
        var lab = CreateLab("lab1", 10, 10, "user1", new Dictionary<string, int>(StringComparer.Ordinal));
        var lab1 = CreateLab("lab2", 11, 11, "user1", new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [ResourceTypes.GhodiumAcid] = 50
        });
        var lab2 = CreateLab("lab3", 12, 12, "user1", new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [ResourceTypes.Catalyst] = 50
        });
        var context = CreateContext([lab, lab1, lab2],
            CreateRunReactionIntent("user1", lab.Id, lab1.Id, lab2.Id), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == lab.Id);
        Assert.Equal(5, Payload.Store![ResourceTypes.CatalyzedGhodiumAcid]);
        Assert.Equal(100 + 80, Payload.CooldownTime);
    }

    [Fact]
    public async Task RunReaction_InsufficientReagent_DoesNothing()
    {
        var lab = CreateLab("lab1", 10, 10, "user1", new Dictionary<string, int>(StringComparer.Ordinal));
        var lab1 = CreateLab("lab2", 11, 11, "user1", new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [ResourceTypes.Hydrogen] = 3
        });
        var lab2 = CreateLab("lab3", 12, 12, "user1", new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [ResourceTypes.Oxygen] = 100
        });
        var context = CreateContext([lab, lab1, lab2],
            CreateRunReactionIntent("user1", lab.Id, lab1.Id, lab2.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task RunReaction_LabOnCooldown_DoesNothing()
    {
        var lab = CreateLab("lab1", 10, 10, "user1", new Dictionary<string, int>(StringComparer.Ordinal), cooldownTime: 200);
        var lab1 = CreateLab("lab2", 11, 11, "user1", new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [ResourceTypes.Hydrogen] = 100
        });
        var lab2 = CreateLab("lab3", 12, 12, "user1", new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [ResourceTypes.Oxygen] = 100
        });
        var context = CreateContext([lab, lab1, lab2],
            CreateRunReactionIntent("user1", lab.Id, lab1.Id, lab2.Id), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task RunReaction_OutOfRange_DoesNothing()
    {
        var lab = CreateLab("lab1", 10, 10, "user1", new Dictionary<string, int>(StringComparer.Ordinal));
        var lab1 = CreateLab("lab2", 20, 20, "user1", new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [ResourceTypes.Hydrogen] = 100
        });
        var lab2 = CreateLab("lab3", 12, 12, "user1", new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [ResourceTypes.Oxygen] = 100
        });
        var context = CreateContext([lab, lab1, lab2],
            CreateRunReactionIntent("user1", lab.Id, lab1.Id, lab2.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task RunReaction_TargetAtCapacity_DoesNothing()
    {
        var lab = CreateLab("lab1", 10, 10, "user1", new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [ResourceTypes.Hydroxide] = 3000
        });
        var lab1 = CreateLab("lab2", 11, 11, "user1", new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [ResourceTypes.Hydrogen] = 100
        });
        var lab2 = CreateLab("lab3", 12, 12, "user1", new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [ResourceTypes.Oxygen] = 100
        });
        var context = CreateContext([lab, lab1, lab2],
            CreateRunReactionIntent("user1", lab.Id, lab1.Id, lab2.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task RunReaction_DifferentMineralInTarget_DoesNothing()
    {
        var lab = CreateLab("lab1", 10, 10, "user1", new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [ResourceTypes.Hydroxide] = 100
        });
        var lab1 = CreateLab("lab2", 11, 11, "user1", new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [ResourceTypes.Zynthium] = 100
        });
        var lab2 = CreateLab("lab3", 12, 12, "user1", new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [ResourceTypes.Hydrogen] = 100
        });
        var context = CreateContext([lab, lab1, lab2],
            CreateRunReactionIntent("user1", lab.Id, lab1.Id, lab2.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task RunReaction_DepletesReagent_ClearsStoreCapacityResource()
    {
        var lab = CreateLab("lab1", 10, 10, "user1", new Dictionary<string, int>(StringComparer.Ordinal));
        var lab1 = CreateLab("lab2", 11, 11, "user1", new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [ResourceTypes.Hydrogen] = 5
        });
        var lab2 = CreateLab("lab3", 12, 12, "user1", new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [ResourceTypes.Oxygen] = 100
        });
        var context = CreateContext([lab, lab1, lab2],
            CreateRunReactionIntent("user1", lab.Id, lab1.Id, lab2.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == lab1.Id);
        Assert.Equal(0, Payload.Store![ResourceTypes.Hydrogen]);
        Assert.NotNull(Payload.StoreCapacityResource);
        Assert.Empty(Payload.StoreCapacityResource);
    }

    [Fact]
    public async Task RunReaction_WithOperateLabEffectLevel1_ProducesBoostedAmount()
    {
        var lab = CreateLabWithEffect("lab1", 10, 10, "user1", new Dictionary<string, int>(StringComparer.Ordinal),
            PowerTypes.OperateLab, level: 1, endTime: 200);
        var lab1 = CreateLab("lab2", 11, 11, "user1", new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [ResourceTypes.Hydrogen] = 100
        });
        var lab2 = CreateLab("lab3", 12, 12, "user1", new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [ResourceTypes.Oxygen] = 100
        });
        var context = CreateContext([lab, lab1, lab2],
            CreateRunReactionIntent("user1", lab.Id, lab1.Id, lab2.Id), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Level 1 effect adds +2 bonus = 7 total reaction amount
        var (_, Payload) = writer.Patches.Single(p => p.ObjectId == lab.Id);
        var expectedAmount = ScreepsGameConstants.LabReactionAmount + 2;
        Assert.Equal(expectedAmount, Payload.Store![ResourceTypes.Hydroxide]);
    }

    [Fact]
    public async Task RunReaction_WithOperateLabEffectLevel3_ProducesBoostedAmount()
    {
        var lab = CreateLabWithEffect("lab1", 10, 10, "user1", new Dictionary<string, int>(StringComparer.Ordinal),
            PowerTypes.OperateLab, level: 3, endTime: 200);
        var lab1 = CreateLab("lab2", 11, 11, "user1", new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [ResourceTypes.Hydrogen] = 100
        });
        var lab2 = CreateLab("lab3", 12, 12, "user1", new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [ResourceTypes.Oxygen] = 100
        });
        var context = CreateContext([lab, lab1, lab2],
            CreateRunReactionIntent("user1", lab.Id, lab1.Id, lab2.Id), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Level 3 effect adds +6 bonus = 11 total reaction amount
        var (_, Payload) = writer.Patches.Single(p => p.ObjectId == lab.Id);
        var expectedAmount = ScreepsGameConstants.LabReactionAmount + 6;
        Assert.Equal(expectedAmount, Payload.Store![ResourceTypes.Hydroxide]);
    }

    [Fact]
    public async Task RunReaction_WithOperateLabEffectLevel5_ProducesBoostedAmount()
    {
        var lab = CreateLabWithEffect("lab1", 10, 10, "user1", new Dictionary<string, int>(StringComparer.Ordinal),
            PowerTypes.OperateLab, level: 5, endTime: 200);
        var lab1 = CreateLab("lab2", 11, 11, "user1", new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [ResourceTypes.Hydrogen] = 100
        });
        var lab2 = CreateLab("lab3", 12, 12, "user1", new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [ResourceTypes.Oxygen] = 100
        });
        var context = CreateContext([lab, lab1, lab2],
            CreateRunReactionIntent("user1", lab.Id, lab1.Id, lab2.Id), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Level 5 effect adds +10 bonus = 15 total reaction amount
        var (_, Payload) = writer.Patches.Single(p => p.ObjectId == lab.Id);
        var expectedAmount = ScreepsGameConstants.LabReactionAmount + 10;
        Assert.Equal(expectedAmount, Payload.Store![ResourceTypes.Hydroxide]);
    }

    #endregion

    #region boostCreep Tests

    [Fact]
    public async Task BoostCreep_BasicBoost_AppliesBoostToPart()
    {
        var lab = CreateLab("lab1", 10, 10, "user1", new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [ResourceTypes.GhodiumHydride] = 100,
            [ResourceTypes.Energy] = 100
        });
        var creep = CreateCreep("creep1", 11, 11, "user1", [new CreepBodyPartSnapshot(BodyPartType.Work, 100, null)]);
        var context = CreateContext([lab, creep],
            CreateBoostCreepIntent("user1", lab.Id, creep.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == creep.Id);
        Assert.NotNull(Payload.Body);
        Assert.Equal(ResourceTypes.GhodiumHydride, Payload.Body![0].Boost);

        var labPatch = writer.Patches.Single(p => p.ObjectId == lab.Id);
        Assert.Equal(70, labPatch.Payload.Store![ResourceTypes.GhodiumHydride]);
        Assert.Equal(80, labPatch.Payload.Store![ResourceTypes.Energy]);
    }

    [Fact]
    public async Task BoostCreep_MultipleParts_BoostsAll()
    {
        var lab = CreateLab("lab1", 10, 10, "user1", new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [ResourceTypes.CatalyzedGhodiumAcid] = 100,
            [ResourceTypes.Energy] = 100
        });
        var creep = CreateCreep("creep1", 11, 11, "user1",
        [
            new CreepBodyPartSnapshot(BodyPartType.Work, 100, null),
            new CreepBodyPartSnapshot(BodyPartType.Work, 100, null),
            new CreepBodyPartSnapshot(BodyPartType.Work, 100, null)
        ]);
        var context = CreateContext([lab, creep],
            CreateBoostCreepIntent("user1", lab.Id, creep.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == creep.Id);
        Assert.Equal(3, Payload.Body!.Count(p => p.Boost == ResourceTypes.CatalyzedGhodiumAcid));

        var labPatch = writer.Patches.Single(p => p.ObjectId == lab.Id);
        Assert.Equal(10, labPatch.Payload.Store![ResourceTypes.CatalyzedGhodiumAcid]);
        Assert.Equal(40, labPatch.Payload.Store![ResourceTypes.Energy]);
    }

    [Fact]
    public async Task BoostCreep_ToughFirst_OrdersCorrectly()
    {
        var lab = CreateLab("lab1", 10, 10, "user1", new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [ResourceTypes.GhodiumOxide] = 100,
            [ResourceTypes.Energy] = 100
        });
        var creep = CreateCreep("creep1", 11, 11, "user1",
        [
            new CreepBodyPartSnapshot(BodyPartType.Work, 100, null),
            new CreepBodyPartSnapshot(BodyPartType.Tough, 100, null),
            new CreepBodyPartSnapshot(BodyPartType.Work, 100, null)
        ]);
        var context = CreateContext([lab, creep],
            CreateBoostCreepIntent("user1", lab.Id, creep.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == creep.Id);
        Assert.Equal(ResourceTypes.GhodiumOxide, Payload.Body![1].Boost);
        Assert.Null(Payload.Body![0].Boost);
        Assert.Null(Payload.Body![2].Boost);
    }

    [Fact]
    public async Task BoostCreep_BodyPartsCountLimit_LimitsBoosts()
    {
        var lab = CreateLab("lab1", 10, 10, "user1", new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [ResourceTypes.GhodiumHydride] = 100,
            [ResourceTypes.Energy] = 100
        });
        var creep = CreateCreep("creep1", 11, 11, "user1",
        [
            new CreepBodyPartSnapshot(BodyPartType.Work, 100, null),
            new CreepBodyPartSnapshot(BodyPartType.Work, 100, null),
            new CreepBodyPartSnapshot(BodyPartType.Work, 100, null),
            new CreepBodyPartSnapshot(BodyPartType.Work, 100, null),
            new CreepBodyPartSnapshot(BodyPartType.Work, 100, null)
        ]);
        var context = CreateContext([lab, creep],
            CreateBoostCreepIntent("user1", lab.Id, creep.Id, bodyPartsCount: 2));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == creep.Id);
        Assert.Equal(2, Payload.Body!.Count(p => p.Boost == ResourceTypes.GhodiumHydride));

        var labPatch = writer.Patches.Single(p => p.ObjectId == lab.Id);
        Assert.Equal(40, labPatch.Payload.Store![ResourceTypes.GhodiumHydride]);
        Assert.Equal(60, labPatch.Payload.Store![ResourceTypes.Energy]);
    }

    [Fact]
    public async Task BoostCreep_InsufficientResources_BoostsPartial()
    {
        var lab = CreateLab("lab1", 10, 10, "user1", new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [ResourceTypes.GhodiumHydride] = 50,
            [ResourceTypes.Energy] = 100
        });
        var creep = CreateCreep("creep1", 11, 11, "user1",
        [
            new CreepBodyPartSnapshot(BodyPartType.Work, 100, null),
            new CreepBodyPartSnapshot(BodyPartType.Work, 100, null),
            new CreepBodyPartSnapshot(BodyPartType.Work, 100, null)
        ]);
        var context = CreateContext([lab, creep],
            CreateBoostCreepIntent("user1", lab.Id, creep.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == creep.Id);
        Assert.Equal(1, Payload.Body!.Count(p => p.Boost == ResourceTypes.GhodiumHydride));

        var labPatch = writer.Patches.Single(p => p.ObjectId == lab.Id);
        Assert.Equal(20, labPatch.Payload.Store![ResourceTypes.GhodiumHydride]);
        Assert.Equal(80, labPatch.Payload.Store![ResourceTypes.Energy]);
    }

    [Fact]
    public async Task BoostCreep_IncompatibleMineral_DoesNothing()
    {
        var lab = CreateLab("lab1", 10, 10, "user1", new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [ResourceTypes.KeaniumOxide] = 100,
            [ResourceTypes.Energy] = 100
        });
        var creep = CreateCreep("creep1", 11, 11, "user1", [new CreepBodyPartSnapshot(BodyPartType.Work, 100, null)]);
        var context = CreateContext([lab, creep],
            CreateBoostCreepIntent("user1", lab.Id, creep.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task BoostCreep_AlreadyBoosted_Skips()
    {
        var lab = CreateLab("lab1", 10, 10, "user1", new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [ResourceTypes.GhodiumHydride] = 100,
            [ResourceTypes.Energy] = 100
        });
        var creep = CreateCreep("creep1", 11, 11, "user1", [new CreepBodyPartSnapshot(BodyPartType.Work, 100, ResourceTypes.GhodiumHydride)]);
        var context = CreateContext([lab, creep],
            CreateBoostCreepIntent("user1", lab.Id, creep.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task BoostCreep_CarryPartsBoost_UpdatesStoreCapacity()
    {
        var lab = CreateLab("lab1", 10, 10, "user1", new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [ResourceTypes.KeaniumAcid] = 100,
            [ResourceTypes.Energy] = 100
        });
        var creep = CreateCreep("creep1", 11, 11, "user1",
        [
            new CreepBodyPartSnapshot(BodyPartType.Carry, 100, null),
            new CreepBodyPartSnapshot(BodyPartType.Carry, 100, null)
        ]);
        var context = CreateContext([lab, creep],
            CreateBoostCreepIntent("user1", lab.Id, creep.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == creep.Id);
        Assert.Equal(2, Payload.Body!.Count(p => p.Boost == ResourceTypes.KeaniumAcid));
        Assert.Equal(300, Payload.StoreCapacity);
    }

    #endregion

    #region unboostCreep Tests

    [Fact]
    public async Task UnboostCreep_SingleBoostedPart_RemovesBoostAndReturnsMinerals()
    {
        var lab = CreateLab("lab1", 10, 10, "user1", new Dictionary<string, int>(StringComparer.Ordinal));
        var creep = CreateCreep("creep1", 11, 11, "user1", [new CreepBodyPartSnapshot(BodyPartType.Work, 100, ResourceTypes.UtriumHydride)]);
        var context = CreateContext([lab, creep],
            CreateUnboostCreepIntent("user1", lab.Id, creep.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (_, creepPayload) = writer.Patches.Single(p => p.ObjectId == creep.Id);
        Assert.NotNull(creepPayload.Body);
        Assert.Single(creepPayload.Body);
        Assert.Null(creepPayload.Body[0].Boost);

        var (_, labPayload) = writer.Patches.Single(p => p.ObjectId == lab.Id);
        Assert.Equal(ScreepsGameConstants.LabUnboostMineral, labPayload.Store![ResourceTypes.UtriumHydride]);
    }

    [Fact]
    public async Task UnboostCreep_MultipleBoostedParts_RemovesAllBoostsAndReturnsMinerals()
    {
        var lab = CreateLab("lab1", 10, 10, "user1", new Dictionary<string, int>(StringComparer.Ordinal));
        var creep = CreateCreep("creep1", 11, 11, "user1",
        [
            new CreepBodyPartSnapshot(BodyPartType.Work, 100, ResourceTypes.UtriumHydride),
            new CreepBodyPartSnapshot(BodyPartType.Work, 100, ResourceTypes.UtriumHydride),
            new CreepBodyPartSnapshot(BodyPartType.Attack, 100, ResourceTypes.UtriumAcid)
        ]);
        var context = CreateContext([lab, creep],
            CreateUnboostCreepIntent("user1", lab.Id, creep.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (_, creepPayload) = writer.Patches.Single(p => p.ObjectId == creep.Id);
        Assert.NotNull(creepPayload.Body);
        Assert.Equal(3, creepPayload.Body.Count);
        Assert.All(creepPayload.Body, part => Assert.Null(part.Boost));

        var (_, labPayload) = writer.Patches.Single(p => p.ObjectId == lab.Id);
        Assert.Equal(2 * ScreepsGameConstants.LabUnboostMineral, labPayload.Store![ResourceTypes.UtriumHydride]);
        Assert.Equal(1 * ScreepsGameConstants.LabUnboostMineral, labPayload.Store[ResourceTypes.UtriumAcid]);
    }

    [Fact]
    public async Task UnboostCreep_BoostedCarryParts_RecalculatesStoreCapacity()
    {
        var lab = CreateLab("lab1", 10, 10, "user1", new Dictionary<string, int>(StringComparer.Ordinal));
        var creep = CreateCreep("creep1", 11, 11, "user1",
        [
            new CreepBodyPartSnapshot(BodyPartType.Carry, 100, ResourceTypes.KeaniumHydride),
            new CreepBodyPartSnapshot(BodyPartType.Carry, 100, ResourceTypes.KeaniumHydride),
            new CreepBodyPartSnapshot(BodyPartType.Work, 100, null)
        ]);
        var context = CreateContext([lab, creep],
            CreateUnboostCreepIntent("user1", lab.Id, creep.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (_, creepPayload) = writer.Patches.Single(p => p.ObjectId == creep.Id);
        Assert.Equal(2 * ScreepsGameConstants.CarryCapacity, creepPayload.StoreCapacity);

        var (_, labPayload) = writer.Patches.Single(p => p.ObjectId == lab.Id);
        Assert.Equal(2 * ScreepsGameConstants.LabUnboostMineral, labPayload.Store![ResourceTypes.KeaniumHydride]);
    }

    [Fact]
    public async Task UnboostCreep_LabTooFar_DoesNothing()
    {
        var lab = CreateLab("lab1", 10, 10, "user1", new Dictionary<string, int>(StringComparer.Ordinal));
        var creep = CreateCreep("creep1", 20, 20, "user1", [new CreepBodyPartSnapshot(BodyPartType.Work, 100, ResourceTypes.UtriumHydride)]);
        var context = CreateContext([lab, creep],
            CreateUnboostCreepIntent("user1", lab.Id, creep.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task UnboostCreep_CreepSpawning_DoesNothing()
    {
        var lab = CreateLab("lab1", 10, 10, "user1", new Dictionary<string, int>(StringComparer.Ordinal));
        var creep = CreateCreep("creep1", 11, 11, "user1", [new CreepBodyPartSnapshot(BodyPartType.Work, 100, ResourceTypes.UtriumHydride)]);
        var spawningCreep = creep with { IsSpawning = true };
        var context = CreateContext([lab, spawningCreep],
            CreateUnboostCreepIntent("user1", lab.Id, creep.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task UnboostCreep_NoBoostedParts_DoesNothing()
    {
        var lab = CreateLab("lab1", 10, 10, "user1", new Dictionary<string, int>(StringComparer.Ordinal));
        var creep = CreateCreep("creep1", 11, 11, "user1", [new CreepBodyPartSnapshot(BodyPartType.Work, 100, null)]);
        var context = CreateContext([lab, creep],
            CreateUnboostCreepIntent("user1", lab.Id, creep.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task UnboostCreep_CreepDoesNotExist_DoesNothing()
    {
        var lab = CreateLab("lab1", 10, 10, "user1", new Dictionary<string, int>(StringComparer.Ordinal));
        var context = CreateContext([lab],
            CreateUnboostCreepIntent("user1", lab.Id, "nonexistent"));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task UnboostCreep_MixedBoostedAndUnboostedParts_OnlyRemovesBoosts()
    {
        var lab = CreateLab("lab1", 10, 10, "user1", new Dictionary<string, int>(StringComparer.Ordinal));
        var creep = CreateCreep("creep1", 11, 11, "user1",
        [
            new CreepBodyPartSnapshot(BodyPartType.Work, 100, ResourceTypes.UtriumHydride),
            new CreepBodyPartSnapshot(BodyPartType.Work, 100, null),
            new CreepBodyPartSnapshot(BodyPartType.Attack, 100, ResourceTypes.UtriumAcid),
            new CreepBodyPartSnapshot(BodyPartType.Move, 100, null)
        ]);
        var context = CreateContext([lab, creep],
            CreateUnboostCreepIntent("user1", lab.Id, creep.Id));
        var writer = (FakeMutationWriter)context.MutationWriter;

        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        var (_, creepPayload) = writer.Patches.Single(p => p.ObjectId == creep.Id);
        Assert.NotNull(creepPayload.Body);
        Assert.Equal(4, creepPayload.Body.Count);
        Assert.All(creepPayload.Body, part => Assert.Null(part.Boost));

        var (_, labPayload) = writer.Patches.Single(p => p.ObjectId == lab.Id);
        Assert.Equal(1 * ScreepsGameConstants.LabUnboostMineral, labPayload.Store![ResourceTypes.UtriumHydride]);
        Assert.Equal(1 * ScreepsGameConstants.LabUnboostMineral, labPayload.Store[ResourceTypes.UtriumAcid]);
    }

    #endregion

    #region Test Helpers

    private static RoomObjectSnapshot CreateLab(string id, int x, int y, string userId, Dictionary<string, int> store, int? cooldownTime = null)
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
            Store: store,
            StoreCapacity: ScreepsGameConstants.LabEnergyCapacity + ScreepsGameConstants.LabMineralCapacity,
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
            CooldownTime: cooldownTime);

    private static RoomObjectSnapshot CreateLabWithEffect(string id, int x, int y, string userId, Dictionary<string, int> store, PowerTypes powerType, int level, int endTime, int? cooldownTime = null)
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
            Store: store,
            StoreCapacity: ScreepsGameConstants.LabEnergyCapacity + ScreepsGameConstants.LabMineralCapacity,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>()
            {
                [powerType] = new(powerType, level, endTime)
            },
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
            MineralAmount: null,
            InvaderHarvested: null,
            Harvested: null,
            Cooldown: null,
            CooldownTime: cooldownTime,
            SafeMode: null,
            SafeModeAvailable: null,
            PortalDestination: null,
            Send: null);

    private static RoomObjectSnapshot CreateCreep(string id, int x, int y, string userId, IReadOnlyList<CreepBodyPartSnapshot> body)
    {
        var storeCapacity = body.Count(p => p.Type == BodyPartType.Carry && p.Hits > 0) * ScreepsGameConstants.CarryCapacity;
        return new RoomObjectSnapshot(
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
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: storeCapacity,
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
    }

    private static RoomIntentSnapshot CreateRunReactionIntent(string userId, string labId, string lab1Id, string lab2Id)
    {
        var argument = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.Lab1] = new(IntentFieldValueKind.Text, TextValue: lab1Id),
            [IntentKeys.Lab2] = new(IntentFieldValueKind.Text, TextValue: lab2Id)
        });

        var record = new IntentRecord(IntentKeys.RunReaction, [argument]);
        var objectIntents = new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal)
        {
            [labId] = [record]
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

        var result = new RoomIntentSnapshot("W1N1", "shard0", users);
        return result;
    }

    private static RoomIntentSnapshot CreateBoostCreepIntent(string userId, string labId, string creepId, int? bodyPartsCount = null)
    {
        var fields = new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: creepId)
        };

        if (bodyPartsCount.HasValue)
            fields[IntentKeys.BodyPartsCount] = new(IntentFieldValueKind.Number, NumberValue: bodyPartsCount.Value);

        var argument = new IntentArgument(fields);

        var record = new IntentRecord(IntentKeys.BoostCreep, [argument]);
        var objectIntents = new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal)
        {
            [labId] = [record]
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

        var result = new RoomIntentSnapshot("W1N1", "shard0", users);
        return result;
    }

    private static RoomIntentSnapshot CreateUnboostCreepIntent(string userId, string labId, string creepId)
    {
        var fields = new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: creepId)
        };

        var argument = new IntentArgument(fields);

        var record = new IntentRecord(IntentKeys.UnboostCreep, [argument]);
        var objectIntents = new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal)
        {
            [labId] = [record]
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

        var result = new RoomIntentSnapshot("W1N1", "shard0", users);
        return result;
    }

    private static RoomProcessorContext CreateContext(IEnumerable<RoomObjectSnapshot> objects, RoomIntentSnapshot? intents = null, int gameTime = 100)
    {
        var objectMap = objects.ToDictionary(o => o.Id, o => o, StringComparer.Ordinal);

        var state = new RoomState(
            "W1N1",
            gameTime,
            null,
            objectMap,
            new Dictionary<string, UserState>(StringComparer.Ordinal),
            intents,
            new Dictionary<string, RoomTerrainSnapshot>(StringComparer.Ordinal),
            []);

        return new RoomProcessorContext(state, new FakeMutationWriter(), new FakeCreepStatsSink());
    }

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

        public Task FlushAsync(CancellationToken token = default) => Task.CompletedTask;

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
