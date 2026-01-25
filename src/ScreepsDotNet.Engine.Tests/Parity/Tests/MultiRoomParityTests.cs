namespace ScreepsDotNet.Engine.Tests.Parity.Tests;

using ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

/// <summary>
/// Multi-room parity tests comparing .NET Engine global processors with Node.js engine.
/// Tests cross-room operations like Terminal.send, Observer.observeRoom, and inter-room transfers.
/// Inherits MongoDB cleanup from ParityTestBase (prevents duplicate key errors).
/// </summary>
[Trait("Category", "Parity")]
[Collection(nameof(Integration.MongoDbParityCollection))]
public sealed class MultiRoomParityTests(Integration.MongoDbParityFixture mongoFixture, Integration.ParityTestPrerequisites prerequisites) : ParityTestBase(mongoFixture)
{
    /// <summary>
    /// Tests Terminal.send parity for cross-room resource transfers.
    /// Validates that .NET Engine produces identical mutations to Node.js engine when processing Terminal.send intents.
    /// Expected behavior:
    /// - Source terminal loses resources (energy cost + transferred amount)
    /// - Target terminal gains transferred resources
    /// - Transaction log created
    /// - Source terminal cooldown applied
    /// </summary>
    [Fact]
    public async Task TerminalSend_MatchesNodeJsEngine()
    {
        var fixturePath = ParityFixturePaths.GetFixturePath("terminal_send.json");
        var globalState = await MultiRoomFixtureLoader.LoadFromFileAsync(fixturePath, TestContext.Current.CancellationToken);

        var dotnetOutput = await DotNetMultiRoomParityTestRunner.RunAsync(globalState, TestContext.Current.CancellationToken);
        var nodejsOutput = await NodeJsParityTestRunner.RunFixtureAsync(fixturePath, prerequisites.HarnessDirectory, MongoFixture.ConnectionString, TestContext.Current.CancellationToken);

        var comparison = Comparison.ParityComparator.CompareMultiRoom(dotnetOutput, nodejsOutput);
        if (comparison.HasDivergences) {
            Assert.Fail(Comparison.DivergenceReporter.FormatReport(comparison, "terminal_send.json"));
        }
    }
}
