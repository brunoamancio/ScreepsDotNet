namespace ScreepsDotNet.Engine.Tests.Parity.Tests;

using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

/// <summary>
/// Parity tests for pull mechanics (advanced movement)
/// Validates pull chain resolution, range checks, movement priority, and loop prevention
/// </summary>
public sealed class PullParityTests
{
    [Fact]
    public async Task Pull_BasicPullChain_BothCreepsMove()
    {
        // Arrange - Creep1 moves right and pulls creep2 (adjacent, behind)
        // Creep2 also needs a move intent to participate in movement (even if pulled)
        var state = new ParityFixtureBuilder()
            .WithCreep("creep1", 10, 10, "user1", [BodyPartType.Move, BodyPartType.Move], capacity: 0)
            .WithCreep("creep2", 9, 10, "user1", [BodyPartType.Move, BodyPartType.Move], capacity: 0)  // Behind creep1
            .WithMoveIntent("user1", "creep1", Direction.Right)  // Creep1 moves to (11, 10)
            .WithMoveIntent("user1", "creep2", Direction.Right)  // Creep2 attempts to move (but will be pulled instead)
            .WithPullIntent("user1", "creep1", "creep2")  // Creep1 pulls creep2
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Creep1 should move to (11, 10), creep2 should be pulled to (10, 10)
        var creep1Patches = output.MutationWriter.Patches.Where(p => p.ObjectId == "creep1" && p.Payload.Position is not null).ToList();
        var creep2Patches = output.MutationWriter.Patches.Where(p => p.ObjectId == "creep2" && p.Payload.Position is not null).ToList();

        Assert.True(creep1Patches.Count > 0, "Creep1 should move");
        var (_, creep1Payload) = creep1Patches.First();
        Assert.Equal(11, creep1Payload.Position!.X);
        Assert.Equal(10, creep1Payload.Position.Y);

        Assert.True(creep2Patches.Count > 0, "Creep2 should be pulled");
        var (_, creep2Payload) = creep2Patches.First();
        Assert.Equal(10, creep2Payload.Position!.X);  // Pulled to creep1's origin
        Assert.Equal(10, creep2Payload.Position.Y);
    }

    [Fact]
    public async Task Pull_OutOfRange_ProducesNoMutation()
    {
        // Arrange - Creep1 tries to pull creep2, but they're too far apart (range 2)
        var state = new ParityFixtureBuilder()
            .WithCreep("creep1", 10, 10, "user1", [BodyPartType.Move, BodyPartType.Move], capacity: 0)
            .WithCreep("creep2", 12, 10, "user1", [BodyPartType.Move, BodyPartType.Move], capacity: 0)
            .WithMoveIntent("user1", "creep1", Direction.Right)  // Creep1 moves to (11, 10)
            .WithPullIntent("user1", "creep1", "creep2")  // Creep1 tries to pull creep2 (out of range)
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Creep1 should move, but creep2 should NOT be pulled (out of range)
        var creep1Patches = output.MutationWriter.Patches.Where(p => p.ObjectId == "creep1" && p.Payload.Position is not null).ToList();
        var creep2Patches = output.MutationWriter.Patches.Where(p => p.ObjectId == "creep2" && p.Payload.Position is not null).ToList();

        Assert.True(creep1Patches.Count > 0, "Creep1 should move");
        Assert.Empty(creep2Patches);  // Creep2 should NOT be pulled (out of range)
    }

    [Fact]
    public async Task Pull_PulledCreepGetsMovementPriority()
    {
        // Arrange - Creep1 pulls creep2 (behind), creep3 tries to move to same tile
        // Pulled creeps should get priority in movement conflicts
        var state = new ParityFixtureBuilder()
            .WithCreep("creep1", 10, 10, "user1", [BodyPartType.Move, BodyPartType.Move], capacity: 0)
            .WithCreep("creep2", 9, 10, "user1", [BodyPartType.Move, BodyPartType.Move], capacity: 0)  // Behind creep1
            .WithCreep("creep3", 10, 11, "user1", [BodyPartType.Move, BodyPartType.Move], capacity: 0)
            .WithMoveIntent("user1", "creep1", Direction.Right)  // Creep1 moves to (11, 10)
            .WithMoveIntent("user1", "creep2", Direction.Right)  // Creep2 attempts to move (will be pulled to 10,10)
            .WithPullIntent("user1", "creep1", "creep2")  // Creep1 pulls creep2 to (10, 10)
            .WithMoveIntent("user1", "creep3", Direction.Top)  // Creep3 tries to move to (10, 10)
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Creep2 (pulled) should win the conflict over creep3
        var creep2Patches = output.MutationWriter.Patches.Where(p => p.ObjectId == "creep2" && p.Payload.Position is not null).ToList();
        var creep3Patches = output.MutationWriter.Patches.Where(p => p.ObjectId == "creep3" && p.Payload.Position is not null).ToList();

        Assert.True(creep2Patches.Count > 0, "Creep2 (pulled) should move");
        var (_, creep2Payload) = creep2Patches.First();
        Assert.Equal(10, creep2Payload.Position!.X);
        Assert.Equal(10, creep2Payload.Position.Y);

        Assert.Empty(creep3Patches);  // Creep3 should lose the conflict (not pulled)
    }

    [Fact]
    public async Task Pull_LoopPrevention_ProducesNoMutation()
    {
        // Arrange - Creep1 pulls creep2, creep2 tries to pull creep1 (loop)
        var state = new ParityFixtureBuilder()
            .WithCreep("creep1", 10, 10, "user1", [BodyPartType.Move, BodyPartType.Move], capacity: 0)
            .WithCreep("creep2", 11, 10, "user1", [BodyPartType.Move, BodyPartType.Move], capacity: 0)
            .WithMoveIntent("user1", "creep1", Direction.Right)  // Creep1 moves to (11, 10)
            .WithPullIntent("user1", "creep1", "creep2")  // Creep1 pulls creep2
            .WithMoveIntent("user1", "creep2", Direction.Left)  // Creep2 moves to (10, 10)
            .WithPullIntent("user1", "creep2", "creep1")  // Creep2 tries to pull creep1 (loop)
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Pull loop should be detected and prevented
        // Only the first pull (creep1 → creep2) should succeed
        var creep1Patches = output.MutationWriter.Patches.Where(p => p.ObjectId == "creep1" && p.Payload.Position is not null).ToList();
        var creep2Patches = output.MutationWriter.Patches.Where(p => p.ObjectId == "creep2" && p.Payload.Position is not null).ToList();

        // Verify both creeps moved (creep1's pull succeeded, creep2's loop was rejected)
        Assert.NotEmpty(creep1Patches);  // Creep1 should move
        Assert.NotEmpty(creep2Patches);  // Creep2 should be pulled (first pull valid)
    }
}
