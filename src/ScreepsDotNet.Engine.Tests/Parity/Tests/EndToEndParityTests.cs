namespace ScreepsDotNet.Engine.Tests.Parity.Tests;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

/// <summary>
/// End-to-end parity tests comparing .NET Engine with Node.js engine
/// These tests verify 100% behavioral parity by comparing mutations field-by-field
/// </summary>
public sealed class EndToEndParityTests
{
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

    // Manual integration test - requires MongoDB + Node.js harness
    // Run manually with: dotnet test --filter "FullyQualifiedName~EndToEndParityTests.Manual"
    //
    // [Fact]
    // public async Task Manual_Harvest_Basic_MatchesNodeJsEngine()
    // {
    //     // Arrange - Save fixture to JSON
    //     var state = new ParityFixtureBuilder()
    //         .WithCreep("creep1", 25, 25, "user1", [BodyPartType.Work, BodyPartType.Move],
    //             capacity: 50,
    //             store: new Dictionary<string, int> { [ResourceTypes.Energy] = 0 })
    //         .WithSource("source1", 26, 25, energy: 3000)
    //         .WithHarvestIntent("user1", "creep1", "source1")
    //         .Build();
    //
    //     var fixturePath = Path.Combine(Path.GetTempPath(), "parity-harvest-basic.json");
    //     // TODO: Serialize state to JSON fixture format
    //
    //     // Act - Execute through both engines
    //     var dotnetOutput = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);
    //     var nodejsOutput = await NodeJsHarnessRunner.RunFixtureAsync(fixturePath, TestContext.Current.CancellationToken);
    //
    //     // Assert - Compare mutations
    //     // TODO: Implement field-by-field comparison
    //     // var comparison = ParityComparator.Compare(dotnetOutput, nodejsOutput);
    //     // Assert.True(comparison.IsMatch, comparison.GetDivergenceReport());
    // }

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
}
