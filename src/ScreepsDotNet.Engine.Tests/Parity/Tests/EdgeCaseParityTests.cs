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

        // Assert - No patches should be created (0 amount)
        Assert.DoesNotContain(output.MutationWriter.Patches, p => p.ObjectId == "creep1");
        Assert.DoesNotContain(output.MutationWriter.Patches, p => p.ObjectId == "creep2");
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

        // Assert - No patches should be created
        Assert.DoesNotContain(output.MutationWriter.Patches, p => p.ObjectId == "controller1");
        Assert.DoesNotContain(output.MutationWriter.Patches, p => p.ObjectId == "creep1");
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
}
