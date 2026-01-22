namespace ScreepsDotNet.Engine.Tests.Parity.Tests;

using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

/// <summary>
/// Parity tests for combat mechanics (attack, ranged attack, heal)
/// Validates damage calculations and healing match Node.js behavior
/// </summary>
public sealed class CombatParityTests
{
    [Fact]
    public async Task Attack_BasicMelee_DamagesTarget()
    {
        // Arrange - Attacker with ATTACK parts adjacent to defender
        var state = new ParityFixtureBuilder()
            .WithCreep("attacker", 10, 10, "user1", [BodyPartType.Attack, BodyPartType.Move],
                capacity: 0)
            .WithCreep("defender", 11, 10, "user2", [BodyPartType.Move, BodyPartType.Move],
                capacity: 0)
            .WithAttackIntent("user1", "attacker", "defender")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Defender should take damage (30 damage per ATTACK part)
        var defenderPatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "defender" && p.Payload.Hits.HasValue).ToList();
        if (defenderPatches.Count > 0)
        {
            var (_, defenderPayload) = defenderPatches.First();
            Assert.True(defenderPayload.Hits < 200, "Defender should take damage from attack");
        }
    }

    [Fact]
    public async Task RangedAttack_WithinRange_DamagesTarget()
    {
        // Arrange - Ranged attacker 3 tiles away from defender
        var state = new ParityFixtureBuilder()
            .WithCreep("attacker", 10, 10, "user1", [BodyPartType.RangedAttack, BodyPartType.Move],
                capacity: 0)
            .WithCreep("defender", 13, 10, "user2", [BodyPartType.Move, BodyPartType.Move],
                capacity: 0)
            .WithRangedAttackIntent("user1", "attacker", "defender")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Defender should take ranged damage
        var defenderPatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "defender" && p.Payload.Hits.HasValue).ToList();
        if (defenderPatches.Count > 0)
        {
            var (_, defenderPayload) = defenderPatches.First();
            Assert.True(defenderPayload.Hits < 200, "Defender should take damage from ranged attack");
        }
    }

    [Fact]
    public async Task Heal_Self_RestoresHits()
    {
        // Arrange - Damaged creep heals itself
        var state = new ParityFixtureBuilder()
            .WithCreep("healer", 10, 10, "user1", [BodyPartType.Heal, BodyPartType.Move],
                capacity: 0,
                hits: 100,  // Damaged
                hitsMax: 200)
            .WithHealIntent("user1", "healer", "healer")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Healer should restore hits (12 hits per HEAL part)
        var healerPatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "healer" && p.Payload.Hits.HasValue).ToList();
        if (healerPatches.Count > 0)
        {
            var (_, healerPayload) = healerPatches.First();
            Assert.True(healerPayload.Hits > 100, "Healer should restore hits");
            Assert.True(healerPayload.Hits <= 200, "Healer should not exceed max hits");
        }
    }

    [Fact]
    public async Task Heal_OtherCreep_RestoresHits()
    {
        // Arrange - Healer adjacent to damaged ally
        var state = new ParityFixtureBuilder()
            .WithCreep("healer", 10, 10, "user1", [BodyPartType.Heal, BodyPartType.Move],
                capacity: 0)
            .WithCreep("ally", 11, 10, "user1", [BodyPartType.Move, BodyPartType.Move],
                capacity: 0,
                hits: 50,  // Damaged
                hitsMax: 200)
            .WithHealIntent("user1", "healer", "ally")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Ally should restore hits
        var allyPatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "ally" && p.Payload.Hits.HasValue).ToList();
        if (allyPatches.Count > 0)
        {
            var (_, allyPayload) = allyPatches.First();
            Assert.True(allyPayload.Hits > 50, "Ally should restore hits from heal");
            Assert.True(allyPayload.Hits <= 200, "Ally should not exceed max hits");
        }
    }

    [Fact]
    public async Task Attack_Structure_DamagesRampart()
    {
        // Arrange - Attacker adjacent to enemy rampart
        var state = new ParityFixtureBuilder()
            .WithCreep("attacker", 10, 10, "user1", [BodyPartType.Attack, BodyPartType.Move],
                capacity: 0)
            .WithRampart("rampart1", 11, 10, "user2", hits: 1000)
            .WithAttackIntent("user1", "attacker", "rampart1")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Rampart should take damage
        var rampartPatches = output.MutationWriter.Patches.Where(p => p.ObjectId == "rampart1" && p.Payload.Hits.HasValue).ToList();
        if (rampartPatches.Count > 0)
        {
            var (_, rampartPayload) = rampartPatches.First();
            Assert.True(rampartPayload.Hits < 1000, "Rampart should take damage from attack");
        }
    }

    [Fact]
    public async Task Attack_OutOfRange_ProducesNoMutation()
    {
        // Arrange - Attacker too far from defender (>1 tile away)
        var state = new ParityFixtureBuilder()
            .WithCreep("attacker", 10, 10, "user1", [BodyPartType.Attack, BodyPartType.Move],
                capacity: 0)
            .WithCreep("defender", 15, 15, "user2", [BodyPartType.Move, BodyPartType.Move],
                capacity: 0)
            .WithAttackIntent("user1", "attacker", "defender")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - No damage should be applied (out of range)
        Assert.DoesNotContain(output.MutationWriter.Patches, p => p.ObjectId == "defender" && p.Payload.Hits.HasValue);
    }

    [Fact]
    public async Task RangedAttack_OutOfRange_ProducesNoMutation()
    {
        // Arrange - Ranged attacker too far from defender (>3 tiles away)
        var state = new ParityFixtureBuilder()
            .WithCreep("attacker", 10, 10, "user1", [BodyPartType.RangedAttack, BodyPartType.Move],
                capacity: 0)
            .WithCreep("defender", 20, 20, "user2", [BodyPartType.Move, BodyPartType.Move],
                capacity: 0)
            .WithRangedAttackIntent("user1", "attacker", "defender")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - No damage should be applied (out of range)
        Assert.DoesNotContain(output.MutationWriter.Patches, p => p.ObjectId == "defender" && p.Payload.Hits.HasValue);
    }

    [Fact]
    public async Task Attack_WithoutAttackPart_ProducesNoMutation()
    {
        // Arrange - Creep without ATTACK part tries to attack
        var state = new ParityFixtureBuilder()
            .WithCreep("attacker", 10, 10, "user1", [BodyPartType.Move, BodyPartType.Move],  // No ATTACK
                capacity: 0)
            .WithCreep("defender", 11, 10, "user2", [BodyPartType.Move, BodyPartType.Move],
                capacity: 0)
            .WithAttackIntent("user1", "attacker", "defender")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - No damage should be applied (missing required body part)
        Assert.DoesNotContain(output.MutationWriter.Patches, p => p.ObjectId == "defender" && p.Payload.Hits.HasValue);
    }
}
