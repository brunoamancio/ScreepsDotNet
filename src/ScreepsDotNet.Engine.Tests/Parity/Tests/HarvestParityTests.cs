namespace ScreepsDotNet.Engine.Tests.Parity.Tests;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

/// <summary>
/// Parity tests for harvest mechanics (E2.2 - Harvest intent family)
/// Phase 2: Basic proof-of-concept with inline fixtures
/// Phase 3+: Will enhance with JSON fixture loading and Node.js comparison
/// </summary>
public sealed class HarvestParityTests
{
    [Fact]
    public async Task HarvestBasic_ExecutesSuccessfully()
    {
        // Arrange - Create simple harvest scenario using builder
        var state = new ParityFixtureBuilder()
            .WithCreep("creep1", 10, 10, "user1", [BodyPartType.Work], capacity: 50)
            .WithSource("source1", 11, 10, energy: 3000)
            .WithHarvestIntent("user1", "creep1", "source1")
            .Build();

        // Act - Run .NET Engine
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Basic smoke test (Phase 3 will add Node.js comparison)
        Assert.NotEmpty(output.MutationWriter.Patches);

        // Find creep patch (should have harvested energy)
        var (_, creepPayload) = output.MutationWriter.Patches.First(p => p.ObjectId == "creep1" && p.Payload.Store is not null);
        var creepEnergy = creepPayload.Store![ResourceTypes.Energy];
        Assert.True(creepEnergy > 0, "Creep should have harvested energy");

        // Find source patch (should have lost energy)
        var (_, sourcePayload) = output.MutationWriter.Patches.First(p => p.ObjectId == "source1" && p.Payload.Energy.HasValue);
        Assert.True(sourcePayload.Energy < 3000, "Source should have lost energy");

        // Stats should be recorded
        Assert.True(output.StatsSink.EnergyHarvested.ContainsKey("user1"), "Energy harvested stat should be recorded");
    }

    [Fact]
    public async Task HarvestWithMultipleWorkParts_HarvestsCorrectAmount()
    {
        // Arrange - Creep with 2 WORK parts should harvest 4 energy
        var state = new ParityFixtureBuilder()
            .WithCreep("creep1", 10, 10, "user1", [BodyPartType.Work, BodyPartType.Work, BodyPartType.Move], capacity: 50)
            .WithSource("source1", 11, 10, energy: 3000)
            .WithHarvestIntent("user1", "creep1", "source1")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert
        var (_, creepPayload) = output.MutationWriter.Patches.First(p => p.ObjectId == "creep1" && p.Payload.Store is not null);
        var (_, sourcePayload) = output.MutationWriter.Patches.First(p => p.ObjectId == "source1" && p.Payload.Energy.HasValue);

        Assert.Equal(4, creepPayload.Store![ResourceTypes.Energy]); // 2 WORK * 2 energy each
        Assert.Equal(2996, sourcePayload.Energy); // 3000 - 4
        Assert.Equal(4, output.StatsSink.EnergyHarvested["user1"]);
    }
}
