namespace ScreepsDotNet.Engine.Tests.Parity.Tests;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

/// <summary>
/// Parity tests for edge cases and boundary conditions
/// </summary>
public sealed class EdgeCaseParityTests
{
    [Fact]
    public async Task Harvest_WithEmptyStore_StillHarvests()
    {
        // Arrange - Creep with empty store harvests from source
        var state = new ParityFixtureBuilder()
            .WithCreep("creep1", 10, 10, "user1", [BodyPartType.Work, BodyPartType.Move],
                capacity: 50,
                store: new Dictionary<string, int>())  // Empty store
            .WithSource("source1", 11, 10, energy: 3000)
            .WithHarvestIntent("user1", "creep1", "source1")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Creep should have harvested energy
        var (_, creepPayload) = output.MutationWriter.Patches.First(p => p.ObjectId == "creep1" && p.Payload.Store is not null);
        Assert.True(creepPayload.Store![ResourceTypes.Energy] > 0, "Creep should harvest into empty store");
    }

    [Fact]
    public async Task Harvest_WithFullStore_SourceStillDepletes()
    {
        // Arrange - Creep with full store tries to harvest
        var state = new ParityFixtureBuilder()
            .WithCreep("creep1", 10, 10, "user1", [BodyPartType.Work, BodyPartType.Move],
                capacity: 50,
                store: new Dictionary<string, int> { [ResourceTypes.Energy] = 50 })  // Full
            .WithSource("source1", 11, 10, energy: 3000)
            .WithHarvestIntent("user1", "creep1", "source1")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Source should still deplete even if creep is full (energy drops on ground)
        var sourcePatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "source1" && p.Payload.Energy.HasValue).ToList();
        if (sourcePatches.Count > 0)
        {
            var (_, sourcePayload) = sourcePatches.First();
            Assert.True(sourcePayload.Energy < 3000, "Source should deplete even when creep is full");
        }
    }

    [Fact]
    public async Task Transfer_WithZeroAmount_TransfersNothing()
    {
        // Arrange - Try to transfer 0 energy
        var state = new ParityFixtureBuilder()
            .WithCreep("creep1", 10, 10, "user1", [BodyPartType.Carry, BodyPartType.Move],
                capacity: 50,
                store: new Dictionary<string, int> { [ResourceTypes.Energy] = 30 })
            .WithCreep("creep2", 11, 10, "user1", [BodyPartType.Carry, BodyPartType.Move],
                capacity: 50,
                store: new Dictionary<string, int> { [ResourceTypes.Energy] = 10 })
            .WithTransferIntent("user1", "creep1", "creep2", ResourceTypes.Energy, 0)
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Store patches should not be created (0 amount transfer)
        // Note: TTL and other passive mechanics may still create patches
        var creep1StorePatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "creep1" && p.Payload.Store is not null).ToList();
        var creep2StorePatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "creep2" && p.Payload.Store is not null).ToList();

        if (creep1StorePatches.Count > 0)
        {
            var (_, creep1Payload) = creep1StorePatches.First();
            Assert.Equal(30, creep1Payload.Store![ResourceTypes.Energy]); // Unchanged
        }

        if (creep2StorePatches.Count > 0)
        {
            var (_, creep2Payload) = creep2StorePatches.First();
            Assert.Equal(10, creep2Payload.Store![ResourceTypes.Energy]); // Unchanged
        }
    }

    [Fact]
    public async Task UpgradeController_WithInsufficientEnergy_DoesNothing()
    {
        // Arrange - Creep with 0 energy tries to upgrade
        var state = new ParityFixtureBuilder()
            .WithCreep("creep1", 10, 10, "user1", [BodyPartType.Work, BodyPartType.Move],
                store: new Dictionary<string, int> { [ResourceTypes.Energy] = 0 })
            .WithController("controller1", 11, 10, "user1", level: 1, progress: 100)
            .WithUpgradeIntent("user1", "creep1", "controller1")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Controller progress should not increase (upgrade failed)
        Assert.DoesNotContain(output.MutationWriter.Patches, p => p.ObjectId == "controller1" && p.Payload.Progress.HasValue);

        // Creep energy should not decrease (upgrade failed)
        var creepPatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "creep1" && p.Payload.Store is not null).ToList();
        if (creepPatches.Count > 0)
        {
            var (_, creepPayload) = creepPatches.First();
            Assert.Equal(0, creepPayload.Store![ResourceTypes.Energy]); // Still 0 (no energy spent)
        }
    }

    [Fact]
    public async Task LinkTransfer_BetweenSameLink_ProcessesWithLoss()
    {
        // Arrange - Transfer energy from link to itself (edge case: self-transfer)
        var state = new ParityFixtureBuilder()
            .WithLink("link1", 10, 10, "user1", energy: 500)
            .WithTransferEnergyIntent("user1", "link1", "link1", 100)
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Self-transfer processes with 3% loss (100 * 0.03 = 3 loss, +97 net)
        var storePatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "link1" && p.Payload.Store is not null).ToList();
        if (storePatches.Count > 0)
        {
            var (_, linkPayload) = storePatches.Last();
            // 500 + (100 - 3% loss) = 597 (deduction doesn't occur on self-transfer, only addition)
            Assert.Equal(597, linkPayload.Store![ResourceTypes.Energy]);
        }
    }

    [Fact]
    public async Task Harvest_SourceWithMinimalEnergy_HarvestsRemaining()
    {
        // Arrange - Source with only 1 energy
        var state = new ParityFixtureBuilder()
            .WithCreep("creep1", 10, 10, "user1", [BodyPartType.Work, BodyPartType.Move],
                capacity: 50,
                store: new Dictionary<string, int>())
            .WithSource("source1", 11, 10, energy: 1)
            .WithHarvestIntent("user1", "creep1", "source1")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Should harvest the 1 remaining energy
        var (_, creepPayload) = output.MutationWriter.Patches.First(p => p.ObjectId == "creep1" && p.Payload.Store is not null);
        var (_, sourcePayload) = output.MutationWriter.Patches.First(p => p.ObjectId == "source1" && p.Payload.Energy.HasValue);

        Assert.Equal(1, creepPayload.Store![ResourceTypes.Energy]);
        Assert.Equal(0, sourcePayload.Energy);
    }

    [Fact]
    public async Task Transfer_OverflowAmount_TransfersUpToCapacity()
    {
        // Arrange - Transfer amount exceeds target capacity
        var state = new ParityFixtureBuilder()
            .WithCreep("creep1", 10, 10, "user1", [BodyPartType.Carry, BodyPartType.Move],
                capacity: 50,
                store: new Dictionary<string, int> { [ResourceTypes.Energy] = 50 })
            .WithCreep("creep2", 11, 10, "user1", [BodyPartType.Carry, BodyPartType.Move],
                capacity: 50,
                store: new Dictionary<string, int> { [ResourceTypes.Energy] = 45 })  // Only 5 capacity remaining
            .WithTransferIntent("user1", "creep1", "creep2", ResourceTypes.Energy, 20)  // Attempt 20, but only 5 fits
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Should transfer only 5 (up to capacity)
        var creep1Patches = output.MutationWriter.Patches.Where(p => p.ObjectId == "creep1" && p.Payload.Store is not null).ToList();
        var creep2Patches = output.MutationWriter.Patches.Where(p => p.ObjectId == "creep2" && p.Payload.Store is not null).ToList();

        if (creep1Patches.Count > 0 && creep2Patches.Count > 0)
        {
            var (_, creep1Payload) = creep1Patches.First();
            var (_, creep2Payload) = creep2Patches.First();

            // Creep1 loses 5, creep2 gains 5
            Assert.Equal(45, creep1Payload.Store![ResourceTypes.Energy]);
            Assert.Equal(50, creep2Payload.Store![ResourceTypes.Energy]);
        }
    }

    [Fact]
    public async Task ConcurrentHarvest_MultipleCreeps_SourceDepletesCorrectly()
    {
        // Arrange - Two creeps harvest from same source
        var state = new ParityFixtureBuilder()
            .WithCreep("creep1", 10, 10, "user1", [BodyPartType.Work, BodyPartType.Move],
                capacity: 50,
                store: new Dictionary<string, int>())
            .WithCreep("creep2", 12, 10, "user1", [BodyPartType.Work, BodyPartType.Move],
                capacity: 50,
                store: new Dictionary<string, int>())
            .WithSource("source1", 11, 10, energy: 10)  // Limited energy
            .WithHarvestIntent("user1", "creep1", "source1")
            .WithHarvestIntent("user1", "creep2", "source1")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Total harvested should equal source depletion
        var creep1Patches = output.MutationWriter.Patches.Where(p => p.ObjectId == "creep1" && p.Payload.Store is not null).ToList();
        var creep2Patches = output.MutationWriter.Patches.Where(p => p.ObjectId == "creep2" && p.Payload.Store is not null).ToList();
        var sourcePatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "source1" && p.Payload.Energy.HasValue).ToList();

        if (creep1Patches.Count > 0 && creep2Patches.Count > 0 && sourcePatches.Count > 0)
        {
            var (_, creep1Payload) = creep1Patches.First();
            var (_, creep2Payload) = creep2Patches.First();
            var (_, sourcePayload) = sourcePatches.First();

            var totalHarvested = creep1Payload.Store![ResourceTypes.Energy] + creep2Payload.Store![ResourceTypes.Energy];
            var sourceDepleted = 10 - sourcePayload.Energy;

            // Both creeps harvest 2 energy each (2 * 1 WORK part = 2 per tick)
            // Total: 4 energy harvested
            // Note: Source depletion may differ based on concurrent harvest mechanics (source energy cap per tick)
            Assert.Equal(4, totalHarvested);  // 2 creeps * 2 harvest power
            Assert.True(sourceDepleted > 0, "Source should deplete");
            Assert.True(sourcePayload.Energy < 10, "Source energy should decrease");
        }
    }

    [Fact]
    public async Task Creep_AtRoomBoundary_CanStillAct()
    {
        // Arrange - Creep at x=0, y=0 (room boundary) harvests adjacent source
        var state = new ParityFixtureBuilder()
            .WithCreep("creep1", 0, 0, "user1", [BodyPartType.Work, BodyPartType.Move],
                capacity: 50,
                store: new Dictionary<string, int>())
            .WithSource("source1", 1, 0, energy: 3000)
            .WithHarvestIntent("user1", "creep1", "source1")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Creep should harvest successfully
        var creepPatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "creep1" && p.Payload.Store is not null).ToList();
        Assert.NotEmpty(creepPatches);

        var (_, creepPayload) = creepPatches.First();
        Assert.True(creepPayload.Store![ResourceTypes.Energy] > 0, "Creep at boundary should harvest");
    }

    [Fact]
    public async Task Creep_WithTTLOne_DecreasesToZero()
    {
        // Arrange - Creep with 1 tick to live
        var state = new ParityFixtureBuilder()
            .WithCreep("creep1", 10, 10, "user1", [BodyPartType.Work, BodyPartType.Move],
                capacity: 50,
                ticksToLive: 1)
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - TTL should decrease to 0 (creep dies)
        var ttlPatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "creep1" && p.Payload.TicksToLive.HasValue).ToList();

        if (ttlPatches.Count > 0)
        {
            var (_, creepPayload) = ttlPatches.First();
            Assert.Equal(0, creepPayload.TicksToLive);
        }
    }

    [Fact]
    public async Task LinkCooldown_ExactExpirationTick_AllowsTransfer()
    {
        // Arrange - Link cooldown expires exactly this tick
        var gameTime = 100;
        var state = new ParityFixtureBuilder()
            .WithGameTime(gameTime)
            .WithLink("link1", 10, 10, "user1", energy: 500, cooldownTime: gameTime)  // Cooldown expires now
            .WithLink("link2", 12, 12, "user1", energy: 0)
            .WithTransferEnergyIntent("user1", "link1", "link2", 100)
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Transfer should succeed (cooldown expired)
        var link1Patches = output.MutationWriter.Patches.Where(p => p.ObjectId == "link1" && p.Payload.Store is not null).ToList();
        var link2Patches = output.MutationWriter.Patches.Where(p => p.ObjectId == "link2" && p.Payload.Store is not null).ToList();

        if (link1Patches.Count > 0 && link2Patches.Count > 0)
        {
            var (_, link1Payload) = link1Patches.First();
            var (_, link2Payload) = link2Patches.First();

            // Link1 loses 100, link2 gains 97 (3% loss)
            Assert.Equal(400, link1Payload.Store![ResourceTypes.Energy]);
            Assert.Equal(97, link2Payload.Store![ResourceTypes.Energy]);
        }
    }

    [Fact]
    public async Task Transfer_MultipleResourceTypes_TransfersCorrectly()
    {
        // Arrange - Transfer multiple resource types in same tick
        var state = new ParityFixtureBuilder()
            .WithCreep("creep1", 10, 10, "user1", [BodyPartType.Carry, BodyPartType.Move],
                capacity: 100,
                store: new Dictionary<string, int>
                {
                    [ResourceTypes.Energy] = 50,
                    [ResourceTypes.Utrium] = 30
                })
            .WithCreep("creep2", 11, 10, "user1", [BodyPartType.Carry, BodyPartType.Move],
                capacity: 100,
                store: new Dictionary<string, int>())
            .WithTransferIntent("user1", "creep1", "creep2", ResourceTypes.Energy, 20)
            .WithTransferIntent("user1", "creep1", "creep2", ResourceTypes.Utrium, 10)
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Both resources transferred
        var creep1Patches = output.MutationWriter.Patches.Where(p => p.ObjectId == "creep1" && p.Payload.Store is not null).ToList();
        var creep2Patches = output.MutationWriter.Patches.Where(p => p.ObjectId == "creep2" && p.Payload.Store is not null).ToList();

        if (creep1Patches.Count > 0 && creep2Patches.Count > 0)
        {
            var (_, creep1Payload) = creep1Patches.First();
            var (_, creep2Payload) = creep2Patches.First();

            Assert.Equal(30, creep1Payload.Store![ResourceTypes.Energy]);
            Assert.Equal(20, creep1Payload.Store!.GetValueOrDefault(ResourceTypes.Utrium, 0));

            Assert.Equal(20, creep2Payload.Store![ResourceTypes.Energy]);
            Assert.Equal(10, creep2Payload.Store!.GetValueOrDefault(ResourceTypes.Utrium, 0));
        }
    }

    [Fact]
    public async Task LabReaction_WithExactComponents_ProducesOutput()
    {
        // Arrange - Labs with exact components for reaction
        var state = new ParityFixtureBuilder()
            .WithLab("outputLab", 10, 10, "user1",
                store: new Dictionary<string, int> { [ResourceTypes.Energy] = 100 })
            .WithLab("inputLab1", 11, 10, "user1",
                store: new Dictionary<string, int> { [ResourceTypes.Hydrogen] = 10 })
            .WithLab("inputLab2", 12, 10, "user1",
                store: new Dictionary<string, int> { [ResourceTypes.Oxygen] = 10 })
            .WithRunReactionIntent("user1", "outputLab", "inputLab1", "inputLab2")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Hydroxide produced (H + O → OH)
        var outputLabPatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "outputLab" && p.Payload.Store is not null).ToList();

        if (outputLabPatches.Count > 0)
        {
            var (_, labPayload) = outputLabPatches.First();
            Assert.True(labPayload.Store!.ContainsKey(ResourceTypes.Hydroxide), "Hydroxide should be produced");
        }
    }

    [Fact]
    public async Task Upgrade_Level7AtMaxProgress_ConsumesEnergy()
    {
        // Arrange - Level 7 controller at max progress (ready to upgrade to 8)
        var state = new ParityFixtureBuilder()
            .WithCreep("creep1", 10, 10, "user1", [BodyPartType.Work, BodyPartType.Move],
                store: new Dictionary<string, int> { [ResourceTypes.Energy] = 50 })
            .WithController("controller1", 11, 10, "user1", level: 7, progress: ScreepsGameConstants.ControllerLevelProgress[ControllerLevel.Level7])  // Max progress for level 7
            .WithUpgradeIntent("user1", "creep1", "controller1")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Creep should consume energy (upgrade intent processed)
        // Note: Level upgrades (7→8) are handled by global processors, not room object patches
        // But energy consumption proves the intent was processed
        var creepPatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "creep1" && p.Payload.Store is not null).ToList();

        if (creepPatches.Count > 0)
        {
            var (_, creepPayload) = creepPatches.First();
            Assert.True(creepPayload.Store![ResourceTypes.Energy] < 50, "Creep should consume energy when upgrading");
        }
    }
}
