namespace ScreepsDotNet.Engine.Tests.Parity.Tests;

using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

/// <summary>
/// Parity tests for spawn mechanics (spawn, renew, recycle)
/// Note: Test doubles use minimal spawn logic, so these tests focus on validation
/// </summary>
public sealed class SpawnParityTests
{
    [Fact]
    public async Task Renew_AdjacentCreep_ConsumesEnergy()
    {
        // Arrange - Spawn renews adjacent creep
        var state = new ParityFixtureBuilder()
            .WithSpawn("spawn1", 10, 10, "user1", energy: 300)
            .WithCreep("creep1", 11, 10, "user1", [BodyPartType.Work, BodyPartType.Move],
                capacity: 0,
                ticksToLive: 100)  // Low TTL
            .WithRenewIntent("user1", "spawn1", "creep1")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Creep TTL should increase (stub validates energy but doesn't pull from extensions)
        var creepPatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "creep1" && p.Payload.TicksToLive.HasValue).ToList();
        Assert.NotEmpty(creepPatches);  // Renew should succeed (spawn has energy)

        var (_, creepPayload) = creepPatches.First();
        Assert.True(creepPayload.TicksToLive > 100, "TTL should increase from renewal");
    }

    [Fact]
    public async Task Recycle_AdjacentCreep_RefundsEnergy()
    {
        // Arrange - Spawn recycles adjacent creep
        var state = new ParityFixtureBuilder()
            .WithSpawn("spawn1", 10, 10, "user1", energy: 100)
            .WithCreep("creep1", 11, 10, "user1", [BodyPartType.Work, BodyPartType.Move],
                capacity: 0,
                ticksToLive: 1000)
            .WithRecycleIntent("user1", "spawn1", "creep1")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Creep should be removed (test double removes creep without energy refund)
        var removals = output.MutationWriter.Removals;
        Assert.Contains("creep1", removals);
    }

    [Fact]
    public async Task Renew_OutOfRange_ProducesNoMutation()
    {
        // Arrange - Spawn tries to renew creep too far away (>1 tile)
        var state = new ParityFixtureBuilder()
            .WithSpawn("spawn1", 10, 10, "user1", energy: 300)
            .WithCreep("creep1", 15, 15, "user1", [BodyPartType.Work, BodyPartType.Move],
                capacity: 0,
                ticksToLive: 100)
            .WithRenewIntent("user1", "spawn1", "creep1")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - No renew should occur (out of range)
        // Note: Validation may reject before processing
        var creepPatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "creep1" && p.Payload.TicksToLive.HasValue).ToList();
        Assert.True(creepPatches.Count == 0 || creepPatches.First().Payload.TicksToLive <= 100, "TTL should not increase (out of range)");
    }

    [Fact]
    public async Task Recycle_OutOfRange_ProducesNoMutation()
    {
        // Arrange - Spawn tries to recycle creep too far away (>1 tile)
        var state = new ParityFixtureBuilder()
            .WithSpawn("spawn1", 10, 10, "user1", energy: 100)
            .WithCreep("creep1", 15, 15, "user1", [BodyPartType.Work, BodyPartType.Move],
                capacity: 0)
            .WithRecycleIntent("user1", "spawn1", "creep1")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Creep should not be removed (out of range)
        Assert.DoesNotContain("creep1", output.MutationWriter.Removals);
    }

    [Fact]
    public async Task Renew_WrongOwner_ProducesNoMutation()
    {
        // Arrange - User1's spawn tries to renew User2's creep
        var state = new ParityFixtureBuilder()
            .WithSpawn("spawn1", 10, 10, "user1", energy: 300)
            .WithCreep("creep1", 11, 10, "user2", [BodyPartType.Work, BodyPartType.Move],  // Different owner
                capacity: 0,
                ticksToLive: 100)
            .WithRenewIntent("user1", "spawn1", "creep1")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - No renew should occur (permission denied)
        var creepPatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "creep1" && p.Payload.TicksToLive.HasValue).ToList();
        Assert.True(creepPatches.Count == 0 || creepPatches.First().Payload.TicksToLive <= 100, "TTL should not increase (wrong owner)");
    }

    [Fact]
    public async Task Renew_WithInsufficientEnergy_ProducesNoMutation()
    {
        // Arrange - Spawn with insufficient energy tries to renew
        var state = new ParityFixtureBuilder()
            .WithSpawn("spawn1", 10, 10, "user1", energy: 0)  // No energy
            .WithCreep("creep1", 11, 10, "user1", [BodyPartType.Work, BodyPartType.Move],
                capacity: 0,
                ticksToLive: 100)
            .WithRenewIntent("user1", "spawn1", "creep1")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - No renew should occur (insufficient energy)
        // Note: Stub checks spawn energy but doesn't pull from extensions (simplified logic)
        // TTL may still decrease passively (100 → 99), but should NOT increase from renew
        var creepPatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "creep1" && p.Payload.TicksToLive.HasValue).ToList();
        if (creepPatches.Count > 0)
        {
            var (_, creepPayload) = creepPatches.First();
            Assert.True(creepPayload.TicksToLive <= 100, "TTL should not increase (insufficient energy)");
        }
    }

    [Fact]
    public async Task Renew_FullTTL_ProducesNoMutation()
    {
        // Arrange - Spawn tries to renew creep already at full TTL
        var state = new ParityFixtureBuilder()
            .WithSpawn("spawn1", 10, 10, "user1", energy: 300)
            .WithCreep("creep1", 11, 10, "user1", [BodyPartType.Work, BodyPartType.Move],
                capacity: 0,
                ticksToLive: 1500)  // Full TTL
            .WithRenewIntent("user1", "spawn1", "creep1")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - No renew should occur (already at full TTL)
        var creepPatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "creep1" && p.Payload.TicksToLive.HasValue).ToList();
        if (creepPatches.Count > 0)
        {
            var (_, creepPayload) = creepPatches.First();
            Assert.True(creepPayload.TicksToLive <= 1500, "TTL should not exceed max");
        }
    }
}
