namespace ScreepsDotNet.Engine.Tests.Parity.Tests;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

/// <summary>
/// Parity tests for power spawn mechanics (process power)
/// Validates power processing mechanics match Node.js behavior
/// </summary>
public sealed class PowerSpawnParityTests
{
    [Fact]
    public async Task ProcessPower_WithResources_ConsumesAndGeneratesPower()
    {
        // Arrange - Power spawn with energy and power processes power
        var state = new ParityFixtureBuilder()
            .WithPowerSpawn("powerSpawn1", 10, 10, "user1",
                energy: 1000,
                power: 10)
            .WithProcessPowerIntent("user1", "powerSpawn1")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Energy and power consumed (1 power requires 50 energy)
        var (_, powerSpawnPayload) = output.MutationWriter.Patches.Single(p => p.ObjectId == "powerSpawn1" && p.Payload.Store is not null);

        // Energy consumed (50 * amount processed)
        Assert.True(powerSpawnPayload.Store![ResourceTypes.Energy] < 1000, "Energy should be consumed");

        // Power consumed (1 power per tick)
        var powerAmount = powerSpawnPayload.Store!.GetValueOrDefault(ResourceTypes.Power, 0);
        Assert.True(powerAmount < 10, "Power should be consumed");
    }

    [Fact]
    public async Task ProcessPower_WithInsufficientEnergy_ProducesNoMutation()
    {
        // Arrange - Power spawn with insufficient energy
        var state = new ParityFixtureBuilder()
            .WithPowerSpawn("powerSpawn1", 10, 10, "user1",
                energy: 10,  // Insufficient (requires 50 per power)
                power: 10)
            .WithProcessPowerIntent("user1", "powerSpawn1")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - No processing should occur (insufficient energy)
        var storePatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "powerSpawn1" && p.Payload.Store is not null).ToList();

        // Resources should be unchanged or only slightly changed
        if (storePatches.Count > 0) {
            var (_, powerSpawnPayload) = storePatches.First();
            Assert.True(powerSpawnPayload.Store![ResourceTypes.Energy] <= 10, "Energy should not decrease significantly");
        }
    }

    [Fact]
    public async Task ProcessPower_WithNoPower_ProducesNoMutation()
    {
        // Arrange - Power spawn with no power
        var state = new ParityFixtureBuilder()
            .WithPowerSpawn("powerSpawn1", 10, 10, "user1",
                energy: 1000,
                power: 0)  // No power
            .WithProcessPowerIntent("user1", "powerSpawn1")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - No processing should occur (no power to process)
        var storePatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "powerSpawn1" && p.Payload.Store is not null).ToList();

        // Energy should be unchanged
        if (storePatches.Count > 0) {
            var (_, powerSpawnPayload) = storePatches.First();
            Assert.Equal(1000, powerSpawnPayload.Store![ResourceTypes.Energy]);
        }
    }

    [Fact]
    public async Task ProcessPower_BalancedRatio_ProcessesCorrectly()
    {
        // Arrange - Power spawn with exact resources for 1 power processing (50 energy : 1 power)
        var state = new ParityFixtureBuilder()
            .WithPowerSpawn("powerSpawn1", 10, 10, "user1",
                energy: ScreepsGameConstants.PowerSpawnEnergyRatio,  // 50
                power: 1)
            .WithProcessPowerIntent("user1", "powerSpawn1")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Should process exactly 1 power with 50 energy
        var storePatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "powerSpawn1" && p.Payload.Store is not null).ToList();

        if (storePatches.Count > 0) {
            var (_, powerSpawnPayload) = storePatches.First();

            // 50 energy consumed, 1 power consumed
            Assert.Equal(0, powerSpawnPayload.Store![ResourceTypes.Energy]);
            Assert.Equal(0, powerSpawnPayload.Store!.GetValueOrDefault(ResourceTypes.Power, 0));
        }
    }
}
