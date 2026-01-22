namespace ScreepsDotNet.Engine.Tests.Parity.Tests;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

/// <summary>
/// End-to-end parity tests comparing .NET Engine with Node.js engine.
/// These tests verify 100% behavioral parity by comparing mutations field-by-field.
///
/// Run with: dotnet test --filter Category=Parity
/// Prerequisites: Node.js 10.13.0+, Docker (both checked automatically)
/// </summary>
[Trait("Category", "Parity")]
[Collection(nameof(Integration.MongoDbParityCollection))]
public sealed class EndToEndParityTests(Integration.MongoDbParityFixture mongoFixture, Integration.ParityTestPrerequisites prerequisites)
{
    // Fixtures injected by xUnit for setup/teardown (suppress unused warnings)
    private readonly Integration.MongoDbParityFixture _ = mongoFixture;
    private readonly Integration.ParityTestPrerequisites __ = prerequisites;

    [Fact]
    public async Task Harvest_Basic_DotNetProducesMutations()
    {
        // Arrange - Create harvest fixture
        var state = new ParityFixtureBuilder()
            .WithCreep("creep1", 25, 25, "user1", [BodyPartType.Work, BodyPartType.Move],
                capacity: 50,
                store: new Dictionary<string, int> { [ResourceTypes.Energy] = 0 })
            .WithSource("source1", 26, 25, energy: 3000)
            .WithHarvestIntent("user1", "creep1", "source1")
            .Build();

        // Act - Execute through .NET Engine
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Verify .NET produces expected mutations
        Assert.NotNull(output);
        Assert.NotEmpty(output.MutationWriter.Patches);

        // Should have at least 2 patches (creep store + source energy)
        Assert.True(output.MutationWriter.Patches.Count >= 2,
            $"Expected at least 2 patches, got {output.MutationWriter.Patches.Count}");

        // Verify creep gained energy
        var creepPatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "creep1").ToList();
        Assert.NotEmpty(creepPatches);

        // Verify source lost energy
        var sourcePatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "source1").ToList();
        Assert.NotEmpty(sourcePatches);
    }

    // ====================================================================================================
    // NODE.JS PARITY INTEGRATION TESTS
    // ====================================================================================================
    // These tests compare .NET Engine against official Node.js Screeps engine.
    // Prerequisites are checked automatically (Node.js, Docker, repos cloned, npm install).
    //
    // Run with: dotnet test --filter Category=Parity
    // ====================================================================================================

    [Fact]
    public async Task Harvest_Basic_MatchesNodeJsEngine()
    {
        // Arrange - Load existing JSON fixture
        var fixturePath = Path.Combine("Parity", "Fixtures", "harvest_basic.json");
        var state = await JsonFixtureLoader.LoadFromFileAsync(fixturePath, TestContext.Current.CancellationToken);

        // Act - Execute through both engines
        var dotnetOutput = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);
        var nodejsOutput = await NodeJsHarnessRunner.RunFixtureAsync(fixturePath, TestContext.Current.CancellationToken);

        // Assert - Compare mutations field-by-field
        var comparison = Comparison.ParityComparator.Compare(dotnetOutput, nodejsOutput);
        if (comparison.HasDivergences) {
            var report = Comparison.DivergenceReporter.FormatReport(comparison, "harvest_basic.json");
            Assert.Fail(report);
        }
    }

    [Fact]
    public async Task Transfer_Basic_DotNetProducesMutations()
    {
        // Arrange - Create transfer fixture
        var state = new ParityFixtureBuilder()
            .WithCreep("creep1", 25, 25, "user1", [BodyPartType.Carry, BodyPartType.Move],
                capacity: 50,
                store: new Dictionary<string, int> { [ResourceTypes.Energy] = 50 })
            .WithSpawn("spawn1", 26, 25, "user1", energy: 200)
            .WithTransferIntent("user1", "creep1", "spawn1", ResourceTypes.Energy, 50)
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEmpty(output.MutationWriter.Patches);

        // Should have patches for creep and spawn
        var creepPatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "creep1").ToList();
        var spawnPatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "spawn1").ToList();

        Assert.NotEmpty(creepPatches);
        Assert.NotEmpty(spawnPatches);
    }

    [Fact]
    public async Task ControllerUpgrade_Basic_DotNetProducesMutations()
    {
        // Arrange
        var state = new ParityFixtureBuilder()
            .WithCreep("creep1", 25, 25, "user1", [BodyPartType.Work, BodyPartType.Carry, BodyPartType.Move],
                capacity: 50,
                store: new Dictionary<string, int> { [ResourceTypes.Energy] = 50 })
            .WithController("controller1", 26, 25, "user1", level: 1, progress: 0, progressTotal: 200)
            .WithUpgradeIntent("user1", "creep1", "controller1")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEmpty(output.MutationWriter.Patches);

        // Should have patches for creep (energy consumed) and controller (progress increased)
        var creepPatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "creep1").ToList();
        var controllerPatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "controller1").ToList();

        Assert.NotEmpty(creepPatches);
        Assert.NotEmpty(controllerPatches);
    }

    [Fact]
    public async Task Transfer_Basic_MatchesNodeJsEngine()
    {
        var fixturePath = Path.Combine("Parity", "Fixtures", "transfer_basic.json");
        var state = await JsonFixtureLoader.LoadFromFileAsync(fixturePath, TestContext.Current.CancellationToken);

        var dotnetOutput = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);
        var nodejsOutput = await NodeJsHarnessRunner.RunFixtureAsync(fixturePath, TestContext.Current.CancellationToken);

        var comparison = Comparison.ParityComparator.Compare(dotnetOutput, nodejsOutput);
        if (comparison.HasDivergences) {
            Assert.Fail(Comparison.DivergenceReporter.FormatReport(comparison, "transfer_basic.json"));
        }
    }

    [Fact]
    public async Task ControllerUpgrade_MatchesNodeJsEngine()
    {
        var fixturePath = Path.Combine("Parity", "Fixtures", "controller_upgrade.json");
        var state = await JsonFixtureLoader.LoadFromFileAsync(fixturePath, TestContext.Current.CancellationToken);

        var dotnetOutput = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);
        var nodejsOutput = await NodeJsHarnessRunner.RunFixtureAsync(fixturePath, TestContext.Current.CancellationToken);

        var comparison = Comparison.ParityComparator.Compare(dotnetOutput, nodejsOutput);
        if (comparison.HasDivergences) {
            Assert.Fail(Comparison.DivergenceReporter.FormatReport(comparison, "controller_upgrade.json"));
        }
    }

    [Fact]
    public async Task LinkTransfer_MatchesNodeJsEngine()
    {
        var fixturePath = Path.Combine("Parity", "Fixtures", "link_transfer.json");
        var state = await JsonFixtureLoader.LoadFromFileAsync(fixturePath, TestContext.Current.CancellationToken);

        var dotnetOutput = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);
        var nodejsOutput = await NodeJsHarnessRunner.RunFixtureAsync(fixturePath, TestContext.Current.CancellationToken);

        var comparison = Comparison.ParityComparator.Compare(dotnetOutput, nodejsOutput);
        if (comparison.HasDivergences) {
            Assert.Fail(Comparison.DivergenceReporter.FormatReport(comparison, "link_transfer.json"));
        }
    }
}
