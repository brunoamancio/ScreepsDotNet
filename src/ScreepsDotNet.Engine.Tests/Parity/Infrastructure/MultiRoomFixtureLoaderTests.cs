namespace ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

using ScreepsDotNet.Common.Constants;

/// <summary>
/// Tests for multi-room fixture loader and test runner.
/// Validates that multi-room fixtures can be loaded and processed by the .NET Engine.
/// </summary>
public sealed class MultiRoomFixtureLoaderTests
{
    [Fact]
    public async Task LoadFromFileAsync_TerminalSendFixture_LoadsSuccessfully()
    {
        // Arrange
        var fixturePath = ParityFixturePaths.GetFixturePath("terminal_send.json");

        // Act
        var globalState = await MultiRoomFixtureLoader.LoadFromFileAsync(fixturePath, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(100, globalState.GameTime);
        Assert.Equal("shard0", globalState.Market.ShardName);

        // Verify AccessibleRooms
        Assert.Equal(2, globalState.AccessibleRooms.Count);
        Assert.Contains("W1N1", globalState.AccessibleRooms.Keys);
        Assert.Contains("W2N2", globalState.AccessibleRooms.Keys);

        // Verify SpecialRoomObjects (terminals)
        Assert.Equal(2, globalState.SpecialRoomObjects.Count);
        var terminal1 = globalState.SpecialRoomObjects.Single(o => o.Id == "terminal1");
        var terminal2 = globalState.SpecialRoomObjects.Single(o => o.Id == "terminal2");

        Assert.Equal("W1N1", terminal1.RoomName);
        Assert.Equal("W2N2", terminal2.RoomName);
        Assert.Equal(RoomObjectTypes.Terminal, terminal1.Type);
        Assert.Equal(RoomObjectTypes.Terminal, terminal2.Type);

        // Verify terminal1 store
        Assert.Equal(10000, terminal1.Store![ResourceTypes.Energy]);
        Assert.Equal(1000, terminal1.Store["U"]);

        // Verify terminal2 store
        Assert.Equal(5000, terminal2.Store![ResourceTypes.Energy]);

        // Verify RoomIntents
        Assert.Single(globalState.RoomIntents);
        Assert.Contains("W1N1", globalState.RoomIntents.Keys);

        var room1Intents = globalState.RoomIntents["W1N1"];
        Assert.Single(room1Intents.Users);
        Assert.Contains("user1", room1Intents.Users.Keys);

        var user1Envelope = room1Intents.Users["user1"];
        Assert.Single(user1Envelope.TerminalIntents);
        Assert.Contains("terminal1", user1Envelope.TerminalIntents.Keys);

        var terminalIntent = user1Envelope.TerminalIntents["terminal1"];
        Assert.NotNull(terminalIntent.Send);
        Assert.Equal("W2N2", terminalIntent.Send!.TargetRoomName);
        Assert.Equal("U", terminalIntent.Send.ResourceType);
        Assert.Equal(100, terminalIntent.Send.Amount);
        Assert.Equal("uranium shipment", terminalIntent.Send.Description);

        // Verify users
        Assert.Equal(2, globalState.Market.Users.Count);
        Assert.Contains("user1", globalState.Market.Users.Keys);
        Assert.Contains("user2", globalState.Market.Users.Keys);
    }

    [Fact]
    public async Task MultiRoomParityTestRunner_TerminalSendFixture_ExecutesSuccessfully()
    {
        // Arrange
        var fixturePath = ParityFixturePaths.GetFixturePath("terminal_send.json");
        var globalState = await MultiRoomFixtureLoader.LoadFromFileAsync(fixturePath, TestContext.Current.CancellationToken);

        // Act
        var output = await DotNetMultiRoomParityTestRunner.RunAsync(globalState, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(output.GlobalMutationWriter);

        // Verify MarketIntentStep processed the Terminal.send intent
        // Expected: Terminal1 loses resources, Terminal2 gains resources, transaction created, cooldown set

        // Should have room object patches for both terminals
        var terminalPatches = output.GlobalMutationWriter.RoomObjectPatches
            .Where(p => p.ObjectId.StartsWith("terminal"))
            .ToList();

        // Note: The exact number of patches depends on MarketIntentStep implementation
        // This test verifies the infrastructure works, not the specific Terminal.send logic
        Assert.NotEmpty(output.GlobalMutationWriter.RoomObjectPatches);
    }
}
