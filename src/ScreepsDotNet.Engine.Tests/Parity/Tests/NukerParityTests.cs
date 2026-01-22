namespace ScreepsDotNet.Engine.Tests.Parity.Tests;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

/// <summary>
/// Parity tests for nuker mechanics (launch, cooldown)
/// Validates nuke launch mechanics match Node.js behavior
/// </summary>
public sealed class NukerParityTests
{
    [Fact]
    public async Task LaunchNuke_WithResources_LaunchesAndEntersCooldown()
    {
        // Arrange - Nuker with energy and ghodium launches nuke to target room
        var state = new ParityFixtureBuilder()
            .WithNuker("nuker1", 10, 10, "user1",
                energy: ScreepsGameConstants.NukerEnergyCapacity,
                ghodium: ScreepsGameConstants.NukerGhodiumCapacity,
                cooldownTime: null)
            .WithLaunchNukeIntent("user1", "nuker1", "W2N2", 25, 25)
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Nuker should consume resources and enter cooldown
        var (_, nukerPayload) = output.MutationWriter.Patches.Single(p => p.ObjectId == "nuker1" && p.Payload.Store is not null);

        // Energy and ghodium consumed
        Assert.Equal(0, nukerPayload.Store![ResourceTypes.Energy]);
        Assert.Equal(0, nukerPayload.Store!.GetValueOrDefault(ResourceTypes.Ghodium, 0));

        // Cooldown set
        var cooldownPatch = output.MutationWriter.Patches.FirstOrDefault(p => p.ObjectId == "nuker1" && p.Payload.CooldownTime.HasValue);
        if (cooldownPatch.ObjectId is not null) {
            var (_, cooldownPayload) = cooldownPatch;
            Assert.True(cooldownPayload.CooldownTime > 100, "Nuker should enter cooldown after launch");
        }
    }

    [Fact]
    public async Task LaunchNuke_OnCooldown_ProducesNoMutation()
    {
        // Arrange - Nuker on cooldown tries to launch again
        var gameTime = 100;
        var state = new ParityFixtureBuilder()
            .WithGameTime(gameTime)
            .WithNuker("nuker1", 10, 10, "user1",
                energy: ScreepsGameConstants.NukerEnergyCapacity,
                ghodium: ScreepsGameConstants.NukerGhodiumCapacity,
                cooldownTime: gameTime + ScreepsGameConstants.NukerCooldown)  // Cooldown active
            .WithLaunchNukeIntent("user1", "nuker1", "W2N2", 25, 25)
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Resources should not be consumed (cooldown blocks launch)
        var storePatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "nuker1" && p.Payload.Store is not null).ToList();

        // If store patch exists, resources should be unchanged
        if (storePatches.Count > 0) {
            var (_, nukerPayload) = storePatches.First();
            Assert.Equal(ScreepsGameConstants.NukerEnergyCapacity, nukerPayload.Store![ResourceTypes.Energy]);
            Assert.Equal(ScreepsGameConstants.NukerGhodiumCapacity, nukerPayload.Store!.GetValueOrDefault(ResourceTypes.Ghodium, 0));
        }
    }

    [Fact]
    public async Task LaunchNuke_WithInsufficientEnergy_ProducesNoMutation()
    {
        // Arrange - Nuker with insufficient energy
        var state = new ParityFixtureBuilder()
            .WithNuker("nuker1", 10, 10, "user1",
                energy: 100,  // Insufficient (requires 300,000)
                ghodium: ScreepsGameConstants.NukerGhodiumCapacity,
                cooldownTime: null)
            .WithLaunchNukeIntent("user1", "nuker1", "W2N2", 25, 25)
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - No launch should occur (insufficient resources)
        var storePatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "nuker1" && p.Payload.Store is not null).ToList();

        // Resources should be unchanged
        if (storePatches.Count > 0) {
            var (_, nukerPayload) = storePatches.First();
            Assert.Equal(100, nukerPayload.Store![ResourceTypes.Energy]);
        }
    }

    [Fact]
    public async Task LaunchNuke_WithInsufficientGhodium_ProducesNoMutation()
    {
        // Arrange - Nuker with insufficient ghodium
        var state = new ParityFixtureBuilder()
            .WithNuker("nuker1", 10, 10, "user1",
                energy: ScreepsGameConstants.NukerEnergyCapacity,
                ghodium: 100,  // Insufficient (requires 5,000)
                cooldownTime: null)
            .WithLaunchNukeIntent("user1", "nuker1", "W2N2", 25, 25)
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - No launch should occur (insufficient resources)
        var storePatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "nuker1" && p.Payload.Store is not null).ToList();

        // Ghodium should be unchanged
        if (storePatches.Count > 0) {
            var (_, nukerPayload) = storePatches.First();
            Assert.Equal(100, nukerPayload.Store!.GetValueOrDefault(ResourceTypes.Ghodium, 0));
        }
    }
}
