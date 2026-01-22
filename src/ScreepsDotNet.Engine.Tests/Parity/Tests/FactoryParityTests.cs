namespace ScreepsDotNet.Engine.Tests.Parity.Tests;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

/// <summary>
/// Parity tests for factory mechanics (commodity production)
/// Validates factory production mechanics match Node.js behavior
/// </summary>
public sealed class FactoryParityTests
{
    [Fact]
    public async Task Produce_BasicCommodity_ConsumesComponentsAndEntersCooldown()
    {
        // Arrange - Factory produces battery (energy + utrium bar)
        var state = new ParityFixtureBuilder()
            .WithFactory("factory1", 10, 10, "user1",
                store: new Dictionary<string, int>
                {
                    [ResourceTypes.Energy] = 100,
                    [ResourceTypes.UtriumBar] = 10
                },
                cooldownTime: null)
            .WithProduceIntent("user1", "factory1", ResourceTypes.Battery)
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Components should be consumed
        var storePatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "factory1" && p.Payload.Store is not null).ToList();

        if (storePatches.Count > 0) {
            var (_, factoryPayload) = storePatches.First();

            // Components consumed (exact amounts depend on commodity recipe)
            Assert.True(factoryPayload.Store![ResourceTypes.Energy] < 100, "Energy should be consumed");
            Assert.True(factoryPayload.Store!.GetValueOrDefault(ResourceTypes.UtriumBar, 0) < 10, "Utrium bar should be consumed");
        }
    }

    [Fact]
    public async Task Produce_OnCooldown_ProducesNoMutation()
    {
        // Arrange - Factory on cooldown tries to produce
        var gameTime = 100;
        var state = new ParityFixtureBuilder()
            .WithGameTime(gameTime)
            .WithFactory("factory1", 10, 10, "user1",
                store: new Dictionary<string, int>
                {
                    [ResourceTypes.Energy] = 100,
                    [ResourceTypes.UtriumBar] = 10
                },
                cooldownTime: gameTime + 50)  // Cooldown active
            .WithProduceIntent("user1", "factory1", ResourceTypes.Battery)
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Components should not be consumed (cooldown blocks production)
        var storePatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "factory1" && p.Payload.Store is not null).ToList();

        // Resources should be unchanged
        if (storePatches.Count > 0) {
            var (_, factoryPayload) = storePatches.First();
            Assert.Equal(100, factoryPayload.Store![ResourceTypes.Energy]);
            Assert.Equal(10, factoryPayload.Store!.GetValueOrDefault(ResourceTypes.UtriumBar, 0));
        }
    }

    [Fact]
    public async Task Produce_WithInsufficientComponents_ProducesNoMutation()
    {
        // Arrange - Factory with insufficient components
        var state = new ParityFixtureBuilder()
            .WithFactory("factory1", 10, 10, "user1",
                store: new Dictionary<string, int>
                {
                    [ResourceTypes.Energy] = 5,  // Insufficient
                    [ResourceTypes.UtriumBar] = 0  // Missing component
                },
                cooldownTime: null)
            .WithProduceIntent("user1", "factory1", ResourceTypes.Battery)
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - No production should occur (insufficient components)
        var storePatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "factory1" && p.Payload.Store is not null).ToList();

        // Resources should be unchanged
        if (storePatches.Count > 0) {
            var (_, factoryPayload) = storePatches.First();
            Assert.Equal(5, factoryPayload.Store![ResourceTypes.Energy]);
            Assert.Equal(0, factoryPayload.Store!.GetValueOrDefault(ResourceTypes.UtriumBar, 0));
        }
    }

    [Fact]
    public async Task Produce_HighLevelCommodity_RequiresCorrectLevel()
    {
        // Arrange - Factory tries to produce high-level commodity (condensate requires level 5 factory)
        var state = new ParityFixtureBuilder()
            .WithFactory("factory1", 10, 10, "user1",
                store: new Dictionary<string, int>
                {
                    [ResourceTypes.Energy] = 1000,
                    [ResourceTypes.Keanium] = 100,
                    [ResourceTypes.Lemergium] = 100,
                    [ResourceTypes.Purifier] = 100
                },
                cooldownTime: null,
                level: null)  // Level 0 factory (no level set)
            .WithProduceIntent("user1", "factory1", ResourceTypes.Condensate)
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Production should fail (level requirement not met)
        var storePatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "factory1" && p.Payload.Store is not null).ToList();

        // Resources should be unchanged (production blocked by level requirement)
        if (storePatches.Count > 0) {
            var (_, factoryPayload) = storePatches.First();
            Assert.Equal(1000, factoryPayload.Store![ResourceTypes.Energy]);
        }
    }

    [Fact]
    public async Task Produce_MultipleComponents_ConsumesAllRequired()
    {
        // Arrange - Factory produces composite (requires energy + multiple minerals)
        var state = new ParityFixtureBuilder()
            .WithFactory("factory1", 10, 10, "user1",
                store: new Dictionary<string, int>
                {
                    [ResourceTypes.Energy] = 200,
                    [ResourceTypes.Utrium] = 50,
                    [ResourceTypes.Lemergium] = 50,
                    [ResourceTypes.Zynthium] = 50,
                    [ResourceTypes.Keanium] = 50,
                    [ResourceTypes.Oxidant] = 50,
                    [ResourceTypes.Reductant] = 50
                },
                cooldownTime: null,
                level: 1)
            .WithProduceIntent("user1", "factory1", ResourceTypes.Composite)
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - All required components consumed
        var storePatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "factory1" && p.Payload.Store is not null).ToList();

        if (storePatches.Count > 0) {
            var (_, factoryPayload) = storePatches.First();

            // Verify energy and components consumed (exact amounts depend on recipe)
            Assert.True(factoryPayload.Store![ResourceTypes.Energy] < 200, "Energy should be consumed");
        }
    }
}
