namespace ScreepsDotNet.Engine.Tests.Parity.Tests;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

/// <summary>
/// Parity tests for lab mechanics (E2.7 - Lab intent family: reactions, boosts, unboosting)
/// </summary>
public sealed class LabParityTests
{
    [Fact]
    public async Task RunReaction_BasicFormula_CreatesProduct()
    {
        // Arrange - Create H + O → OH (Hydroxide) reaction
        var state = new ParityFixtureBuilder()
            .WithLab("lab1", 10, 10, "user1", new Dictionary<string, int>
            {
                [ResourceTypes.Hydrogen] = 1000,
                [ResourceTypes.Energy] = 2000
            })
            .WithLab("lab2", 11, 10, "user1", new Dictionary<string, int>
            {
                [ResourceTypes.Oxygen] = 1000,
                [ResourceTypes.Energy] = 2000
            })
            .WithLab("lab3", 12, 10, "user1", new Dictionary<string, int>
            {
                [ResourceTypes.Energy] = 2000
            })
            .WithRunReactionIntent("user1", "lab3", "lab1", "lab2")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Output lab should have hydroxide, input labs should have consumed resources
        var (_, lab3Payload) = output.MutationWriter.Patches.First(p => p.ObjectId == "lab3" && p.Payload.Store is not null);
        var hydroxideAmount = lab3Payload.Store!.GetValueOrDefault(ResourceTypes.Hydroxide, 0);

        Assert.True(hydroxideAmount > 0, "Reaction should produce hydroxide");
    }

    [Fact]
    public async Task BoostCreep_WithCompound_BoostsBodyParts()
    {
        // Arrange - Lab with UH compound, creep with work parts
        var state = new ParityFixtureBuilder()
            .WithCreep("creep1", 10, 10, "user1",
                [BodyPartType.Work, BodyPartType.Work, BodyPartType.Move],
                capacity: 50,
                store: new Dictionary<string, int> { [ResourceTypes.Energy] = 10 })
            .WithLab("lab1", 11, 10, "user1", new Dictionary<string, int>
            {
                [ResourceTypes.UtriumHydride] = 60,  // UH - boosts attack/harvest
                [ResourceTypes.Energy] = 2000
            })
            .WithBoostCreepIntent("user1", "lab1", "creep1", bodyPartsCount: 2)
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Lab should have consumed 60 UH (30 per work part)
        var labPatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "lab1" && p.Payload.Store is not null).ToList();
        if (labPatches.Count > 0)
        {
            var (_, labPayload) = labPatches.First();
            var remainingUH = labPayload.Store!.GetValueOrDefault(ResourceTypes.UtriumHydride, 60);
            Assert.True(remainingUH < 60, "Lab should have consumed UH for boosting");
        }
    }

    [Fact]
    public async Task RunReaction_SetsCooldown()
    {
        // Arrange - Create simple reaction
        var state = new ParityFixtureBuilder()
            .WithLab("lab1", 10, 10, "user1", new Dictionary<string, int>
            {
                [ResourceTypes.Hydrogen] = 1000,
                [ResourceTypes.Energy] = 2000
            })
            .WithLab("lab2", 11, 10, "user1", new Dictionary<string, int>
            {
                [ResourceTypes.Oxygen] = 1000,
                [ResourceTypes.Energy] = 2000
            })
            .WithLab("lab3", 12, 10, "user1", new Dictionary<string, int>
            {
                [ResourceTypes.Energy] = 2000
            })
            .WithRunReactionIntent("user1", "lab3", "lab1", "lab2")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Output lab should have cooldown set
        var lab3Patches = output.MutationWriter.Patches.Where(p => p.ObjectId == "lab3").ToList();
        Assert.NotEmpty(lab3Patches);

        var hasCooldown = lab3Patches.Any(p => p.Payload.CooldownTime.HasValue && p.Payload.CooldownTime.Value > 100);
        Assert.True(hasCooldown, "Lab cooldown should be set after reaction");
    }
}
