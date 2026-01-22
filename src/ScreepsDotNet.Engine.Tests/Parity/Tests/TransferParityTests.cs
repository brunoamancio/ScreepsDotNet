namespace ScreepsDotNet.Engine.Tests.Parity.Tests;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

/// <summary>
/// Parity tests for resource transfer mechanics (E2.4 - Transfer/withdraw intent family)
/// </summary>
public sealed class TransferParityTests
{
    [Fact]
    public async Task TransferEnergy_BetweenCreeps_MovesResourceCorrectly()
    {
        // Arrange
        var state = new ParityFixtureBuilder()
            .WithCreep("creep1", 10, 10, "user1", [BodyPartType.Carry, BodyPartType.Move],
                capacity: 50,
                store: new Dictionary<string, int> { [ResourceTypes.Energy] = 30 })
            .WithCreep("creep2", 11, 10, "user1", [BodyPartType.Carry, BodyPartType.Move],
                capacity: 50,
                store: new Dictionary<string, int> { [ResourceTypes.Energy] = 10 })
            .WithTransferIntent("user1", "creep1", "creep2", ResourceTypes.Energy, 15)
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert
        var (_, creep1Payload) = output.MutationWriter.Patches.First(p => p.ObjectId == "creep1" && p.Payload.Store is not null);
        var (_, creep2Payload) = output.MutationWriter.Patches.First(p => p.ObjectId == "creep2" && p.Payload.Store is not null);

        Assert.Equal(15, creep1Payload.Store![ResourceTypes.Energy]); // 30 - 15
        Assert.Equal(25, creep2Payload.Store![ResourceTypes.Energy]); // 10 + 15
    }

    [Fact]
    public async Task TransferEnergy_MoreThanAvailable_TransfersMaximum()
    {
        // Arrange - Try to transfer 50, but only 30 available
        var state = new ParityFixtureBuilder()
            .WithCreep("creep1", 10, 10, "user1", [BodyPartType.Carry, BodyPartType.Move],
                capacity: 50,
                store: new Dictionary<string, int> { [ResourceTypes.Energy] = 30 })
            .WithCreep("creep2", 11, 10, "user1", [BodyPartType.Carry, BodyPartType.Move],
                capacity: 50,
                store: new Dictionary<string, int> { [ResourceTypes.Energy] = 0 })
            .WithTransferIntent("user1", "creep1", "creep2", ResourceTypes.Energy, 50)
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert
        var (_, creep1Payload) = output.MutationWriter.Patches.First(p => p.ObjectId == "creep1" && p.Payload.Store is not null);
        var (_, creep2Payload) = output.MutationWriter.Patches.First(p => p.ObjectId == "creep2" && p.Payload.Store is not null);

        Assert.Equal(0, creep1Payload.Store![ResourceTypes.Energy]); // All 30 transferred
        Assert.Equal(30, creep2Payload.Store![ResourceTypes.Energy]); // Received 30 (not 50)
    }

    [Fact]
    public async Task TransferEnergy_TargetFull_TransfersUntilFull()
    {
        // Arrange - Target has capacity 50, currently has 45, trying to transfer 10
        var state = new ParityFixtureBuilder()
            .WithCreep("creep1", 10, 10, "user1", [BodyPartType.Carry, BodyPartType.Move],
                capacity: 50,
                store: new Dictionary<string, int> { [ResourceTypes.Energy] = 20 })
            .WithCreep("creep2", 11, 10, "user1", [BodyPartType.Carry, BodyPartType.Move],
                capacity: 50,
                store: new Dictionary<string, int> { [ResourceTypes.Energy] = 45 })
            .WithTransferIntent("user1", "creep1", "creep2", ResourceTypes.Energy, 10)
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert
        var (_, creep1Payload) = output.MutationWriter.Patches.First(p => p.ObjectId == "creep1" && p.Payload.Store is not null);
        var (_, creep2Payload) = output.MutationWriter.Patches.First(p => p.ObjectId == "creep2" && p.Payload.Store is not null);

        Assert.Equal(15, creep1Payload.Store![ResourceTypes.Energy]); // 20 - 5 (only 5 could fit)
        Assert.Equal(50, creep2Payload.Store![ResourceTypes.Energy]); // 45 + 5 = 50 (full)
    }
}
