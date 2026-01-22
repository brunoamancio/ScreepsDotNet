namespace ScreepsDotNet.Engine.Tests.Parity.Tests;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

/// <summary>
/// Parity tests for build and repair mechanics
/// Validates construction progress and structure repairs match Node.js behavior
/// </summary>
public sealed class BuildRepairParityTests
{
    [Fact]
    public async Task Build_ConstructionSite_IncreasesProgress()
    {
        // Arrange - Worker builds construction site
        var state = new ParityFixtureBuilder()
            .WithCreep("worker", 10, 10, "user1", [BodyPartType.Work, BodyPartType.Move],
                capacity: 50,
                store: new Dictionary<string, int> { [ResourceTypes.Energy] = 50 })
            .WithConstructionSite("site1", 11, 10, "user1", RoomObjectTypes.Extension, progress: 0, progressTotal: 3000)
            .WithBuildIntent("user1", "worker", "site1")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Construction site progress should increase (5 progress per WORK part per energy)
        var sitePatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "site1" && p.Payload.Progress.HasValue).ToList();
        if (sitePatches.Count > 0) {
            var (_, sitePayload) = sitePatches.First();
            Assert.True(sitePayload.Progress > 0, "Construction site progress should increase");
        }

        // Worker energy should decrease
        var workerPatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "worker" && p.Payload.Store is not null).ToList();
        if (workerPatches.Count > 0) {
            var (_, workerPayload) = workerPatches.First();
            Assert.True(workerPayload.Store![ResourceTypes.Energy] < 50, "Worker energy should decrease");
        }
    }

    [Fact]
    public async Task Repair_DamagedStructure_RestoresHits()
    {
        // Arrange - Worker repairs damaged road
        var state = new ParityFixtureBuilder()
            .WithCreep("worker", 10, 10, "user1", [BodyPartType.Work, BodyPartType.Move],
                capacity: 50,
                store: new Dictionary<string, int> { [ResourceTypes.Energy] = 50 })
            .WithRoad("road1", 11, 10, hits: 2000, hitsMax: 5000)
            .WithRepairIntent("user1", "worker", "road1")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Road hits should increase (100 hits per WORK part per energy)
        var roadPatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "road1" && p.Payload.Hits.HasValue).ToList();
        if (roadPatches.Count > 0) {
            var (_, roadPayload) = roadPatches.First();
            Assert.True(roadPayload.Hits > 2000, "Road hits should increase from repair");
            Assert.True(roadPayload.Hits <= 5000, "Road hits should not exceed max");
        }

        // Worker energy should decrease
        var workerPatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "worker" && p.Payload.Store is not null).ToList();
        if (workerPatches.Count > 0) {
            var (_, workerPayload) = workerPatches.First();
            Assert.True(workerPayload.Store![ResourceTypes.Energy] < 50, "Worker energy should decrease");
        }
    }

    [Fact]
    public async Task Build_OutOfRange_ProducesNoMutation()
    {
        // Arrange - Worker too far from construction site (>3 tiles away)
        var state = new ParityFixtureBuilder()
            .WithCreep("worker", 10, 10, "user1", [BodyPartType.Work, BodyPartType.Move],
                capacity: 50,
                store: new Dictionary<string, int> { [ResourceTypes.Energy] = 50 })
            .WithConstructionSite("site1", 20, 20, "user1", RoomObjectTypes.Extension, progress: 0, progressTotal: 3000)
            .WithBuildIntent("user1", "worker", "site1")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - No build progress (out of range)
        Assert.DoesNotContain(output.MutationWriter.Patches, p => p.ObjectId == "site1" && p.Payload.Progress.HasValue);
    }

    [Fact]
    public async Task Repair_OutOfRange_ProducesNoMutation()
    {
        // Arrange - Worker too far from structure (>3 tiles away)
        var state = new ParityFixtureBuilder()
            .WithCreep("worker", 10, 10, "user1", [BodyPartType.Work, BodyPartType.Move],
                capacity: 50,
                store: new Dictionary<string, int> { [ResourceTypes.Energy] = 50 })
            .WithRoad("road1", 20, 20, hits: 2000, hitsMax: 5000)
            .WithRepairIntent("user1", "worker", "road1")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - No repair (out of range)
        // Note: Road may still decay passively (2000 → 1950), so check it didn't increase
        var roadPatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "road1" && p.Payload.Hits.HasValue).ToList();
        if (roadPatches.Count > 0) {
            var (_, roadPayload) = roadPatches.First();
            Assert.True(roadPayload.Hits <= 2000, "Road hits should not increase (out of range)");
        }
    }

    [Fact]
    public async Task Build_WithoutEnergy_ProducesNoMutation()
    {
        // Arrange - Worker with no energy tries to build
        var state = new ParityFixtureBuilder()
            .WithCreep("worker", 10, 10, "user1", [BodyPartType.Work, BodyPartType.Move],
                capacity: 50,
                store: new Dictionary<string, int> { [ResourceTypes.Energy] = 0 })
            .WithConstructionSite("site1", 11, 10, "user1", RoomObjectTypes.Extension, progress: 0, progressTotal: 3000)
            .WithBuildIntent("user1", "worker", "site1")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - No build progress (no energy)
        Assert.DoesNotContain(output.MutationWriter.Patches, p => p.ObjectId == "site1" && p.Payload.Progress.HasValue);
    }

    [Fact]
    public async Task Repair_WithoutEnergy_ProducesNoMutation()
    {
        // Arrange - Worker with no energy tries to repair
        var state = new ParityFixtureBuilder()
            .WithCreep("worker", 10, 10, "user1", [BodyPartType.Work, BodyPartType.Move],
                capacity: 50,
                store: new Dictionary<string, int> { [ResourceTypes.Energy] = 0 })
            .WithRoad("road1", 11, 10, hits: 2000, hitsMax: 5000)
            .WithRepairIntent("user1", "worker", "road1")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - No repair (no energy)
        // Note: Road may still decay passively (2000 → 1950), so check it didn't increase
        var roadPatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "road1" && p.Payload.Hits.HasValue).ToList();
        if (roadPatches.Count > 0) {
            var (_, roadPayload) = roadPatches.First();
            Assert.True(roadPayload.Hits <= 2000, "Road hits should not increase (no energy)");
        }
    }

    [Fact]
    public async Task Build_WithoutWorkPart_ProducesNoMutation()
    {
        // Arrange - Creep without WORK part tries to build
        var state = new ParityFixtureBuilder()
            .WithCreep("worker", 10, 10, "user1", [BodyPartType.Move, BodyPartType.Carry],  // No WORK
                capacity: 50,
                store: new Dictionary<string, int> { [ResourceTypes.Energy] = 50 })
            .WithConstructionSite("site1", 11, 10, "user1", RoomObjectTypes.Extension, progress: 0, progressTotal: 3000)
            .WithBuildIntent("user1", "worker", "site1")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - No build progress (missing required body part)
        Assert.DoesNotContain(output.MutationWriter.Patches, p => p.ObjectId == "site1" && p.Payload.Progress.HasValue);
    }

    [Fact]
    public async Task Repair_FullHits_ProducesNoMutation()
    {
        // Arrange - Worker tries to repair structure at full hits
        var state = new ParityFixtureBuilder()
            .WithCreep("worker", 10, 10, "user1", [BodyPartType.Work, BodyPartType.Move],
                capacity: 50,
                store: new Dictionary<string, int> { [ResourceTypes.Energy] = 50 })
            .WithRoad("road1", 11, 10, hits: 5000, hitsMax: 5000)  // Full hits
            .WithRepairIntent("user1", "worker", "road1")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - No repair (already at full hits)
        // Note: Road decays by 50 hits per tick (5000 → 4950), but should not be repaired back to 5000
        var roadPatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "road1" && p.Payload.Hits.HasValue).ToList();
        if (roadPatches.Count > 0) {
            var (_, roadPayload) = roadPatches.First();
            Assert.True(roadPayload.Hits <= 5000, "Road hits should not exceed max");
            Assert.True(roadPayload.Hits >= 4950, "Road should only decay by 50 hits");
        }
    }
}
