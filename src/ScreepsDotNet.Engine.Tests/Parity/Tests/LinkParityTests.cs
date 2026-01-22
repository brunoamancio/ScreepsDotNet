namespace ScreepsDotNet.Engine.Tests.Parity.Tests;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

/// <summary>
/// Parity tests for link energy transfer mechanics (E2.7 - Link intent family)
/// </summary>
public sealed class LinkParityTests
{
    [Fact]
    public async Task TransferEnergy_BetweenLinks_TransfersCorrectAmount()
    {
        // Arrange - Transfer 400 energy from link1 to link2 (3% loss = 12 energy)
        var state = new ParityFixtureBuilder()
            .WithLink("link1", 10, 10, "user1", energy: 500)
            .WithLink("link2", 15, 15, "user1", energy: 100)
            .WithTransferEnergyIntent("user1", "link1", "link2", 400)
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - 400 energy requested, 3% loss = 12 energy lost, 388 delivered
        var (_, link1Payload) = output.MutationWriter.Patches.First(p => p.ObjectId == "link1" && p.Payload.Store is not null);
        var (_, link2Payload) = output.MutationWriter.Patches.First(p => p.ObjectId == "link2" && p.Payload.Store is not null);

        Assert.Equal(100, link1Payload.Store![ResourceTypes.Energy]); // 500 - 400
        Assert.Equal(488, link2Payload.Store![ResourceTypes.Energy]); // 100 + 388 (after 3% loss)
    }

    [Fact]
    public async Task TransferEnergy_SourceLinkEmpty_TransfersNothing()
    {
        // Arrange - Source link has 0 energy
        var state = new ParityFixtureBuilder()
            .WithLink("link1", 10, 10, "user1", energy: 0)
            .WithLink("link2", 15, 15, "user1", energy: 100)
            .WithTransferEnergyIntent("user1", "link1", "link2", 400)
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - No patches should be created (no energy to transfer)
        Assert.DoesNotContain(output.MutationWriter.Patches, p => p.ObjectId == "link1");
        Assert.DoesNotContain(output.MutationWriter.Patches, p => p.ObjectId == "link2");
    }

    [Fact]
    public async Task TransferEnergy_TargetLinkFull_TransfersToCapacity()
    {
        // Arrange - Target link at 780/800 capacity, trying to transfer 100
        var state = new ParityFixtureBuilder()
            .WithLink("link1", 10, 10, "user1", energy: 500)
            .WithLink("link2", 15, 15, "user1", energy: 780)
            .WithTransferEnergyIntent("user1", "link1", "link2", 100)
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Only 20 energy can fit, accounting for 3% loss
        var (_, link1Payload) = output.MutationWriter.Patches.First(p => p.ObjectId == "link1" && p.Payload.Store is not null);
        var (_, link2Payload) = output.MutationWriter.Patches.First(p => p.ObjectId == "link2" && p.Payload.Store is not null);

        var link2Final = link2Payload.Store![ResourceTypes.Energy];
        Assert.True(link2Final is >= 799 and <= 800, $"Expected link2 at or near capacity (799-800), got {link2Final}");
        Assert.True(link1Payload.Store![ResourceTypes.Energy] <= 500); // Source decreased
    }

    [Fact]
    public async Task TransferEnergy_SetsCooldown()
    {
        // Arrange - Transfer energy and verify cooldown is set
        var state = new ParityFixtureBuilder()
            .WithLink("link1", 10, 10, "user1", energy: 500)
            .WithLink("link2", 15, 15, "user1", energy: 100)
            .WithTransferEnergyIntent("user1", "link1", "link2", 400)
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Source link should have cooldown set (Cooldown property, not CooldownTime)
        var link1Patches = output.MutationWriter.Patches.Where(p => p.ObjectId == "link1").ToList();
        Assert.NotEmpty(link1Patches);

        var hasCooldown = link1Patches.Any(p => p.Payload.Cooldown.HasValue && p.Payload.Cooldown.Value > 0);
        Assert.True(hasCooldown, "Link cooldown should be set after transfer (countdown ticker)");
    }
}
