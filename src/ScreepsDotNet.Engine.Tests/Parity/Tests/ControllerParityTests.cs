namespace ScreepsDotNet.Engine.Tests.Parity.Tests;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

/// <summary>
/// Parity tests for controller mechanics (E2.5 - Controller intent family)
/// </summary>
public sealed class ControllerParityTests
{
    [Fact]
    public async Task UpgradeController_WithSingleWorkPart_IncreasesProgress()
    {
        // Arrange
        var state = new ParityFixtureBuilder()
            .WithCreep("creep1", 10, 10, "user1", [BodyPartType.Work, BodyPartType.Move],
                store: new Dictionary<string, int> { [ResourceTypes.Energy] = 50 })
            .WithController("controller1", 11, 10, "user1", level: 1, progress: 100)
            .WithUpgradeIntent("user1", "creep1", "controller1")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert
        var (_, controllerPayload) = output.MutationWriter.Patches.First(p => p.ObjectId == "controller1" && p.Payload.Progress.HasValue);
        var (_, creepPayload) = output.MutationWriter.Patches.First(p => p.ObjectId == "creep1" && p.Payload.Store is not null);

        Assert.Equal(101, controllerPayload.Progress); // 100 + 1 (1 WORK * 1 energy)
        Assert.Equal(49, creepPayload.Store![ResourceTypes.Energy]); // 50 - 1
        Assert.Equal(1, output.StatsSink.EnergyControl["user1"]);
    }

    [Fact]
    public async Task UpgradeController_WithMultipleWorkParts_ScalesCorrectly()
    {
        // Arrange - 3 WORK parts should use 3 energy
        var state = new ParityFixtureBuilder()
            .WithCreep("creep1", 10, 10, "user1",
                [BodyPartType.Work, BodyPartType.Work, BodyPartType.Work, BodyPartType.Move, BodyPartType.Move],
                store: new Dictionary<string, int> { [ResourceTypes.Energy] = 50 })
            .WithController("controller1", 11, 10, "user1", level: 2, progress: 500)
            .WithUpgradeIntent("user1", "creep1", "controller1")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert
        var (_, controllerPayload) = output.MutationWriter.Patches.First(p => p.ObjectId == "controller1" && p.Payload.Progress.HasValue);
        var (_, creepPayload) = output.MutationWriter.Patches.First(p => p.ObjectId == "creep1" && p.Payload.Store is not null);

        Assert.Equal(503, controllerPayload.Progress); // 500 + 3 (3 WORK * 1 energy each)
        Assert.Equal(47, creepPayload.Store![ResourceTypes.Energy]); // 50 - 3
        Assert.Equal(3, output.StatsSink.EnergyControl["user1"]);
    }

    [Fact]
    public async Task UpgradeController_WithNoEnergy_DoesNothing()
    {
        // Arrange - Creep has no energy
        var state = new ParityFixtureBuilder()
            .WithCreep("creep1", 10, 10, "user1", [BodyPartType.Work, BodyPartType.Move],
                store: new Dictionary<string, int> { [ResourceTypes.Energy] = 0 })
            .WithController("controller1", 11, 10, "user1", level: 1, progress: 100)
            .WithUpgradeIntent("user1", "creep1", "controller1")
            .Build();

        // Act
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - No patches should be created
        Assert.DoesNotContain(output.MutationWriter.Patches, p => p.ObjectId == "controller1");
        Assert.False(output.StatsSink.EnergyControl.ContainsKey("user1"));
    }
}
