namespace ScreepsDotNet.Engine.Tests.Parity.Tests;

using System.Text.Json;
using ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

/// <summary>
/// Multi-room parity tests comparing .NET Engine global processors with Node.js engine.
/// Tests cross-room operations like Terminal.send, PowerCreep management, and inter-room transfers.
/// All tests follow the same pattern: load fixture, run both engines, compare outputs.
/// New tests are automatically discovered from multi-room JSON fixtures in the Fixtures directory.
/// Inherits MongoDB cleanup from ParityTestBase (prevents duplicate key errors).
/// </summary>
[Trait("Category", "Parity")]
[Collection(nameof(Integration.MongoDbParityCollection))]
public sealed class MultiRoomParityTests(Integration.MongoDbParityFixture mongoFixture, Integration.ParityTestPrerequisites prerequisites) : ParityTestBase(mongoFixture)
{
    /// <summary>
    /// Auto-discovers all multi-room JSON fixture files in the Parity/Fixtures directory.
    /// New fixtures are automatically included without code changes.
    /// A fixture is considered multi-room if it contains a "rooms" property.
    /// </summary>
    public static TheoryData<string> AllMultiRoomFixtures()
    {
        var fixturesDir = Path.Combine("Parity", "Fixtures");
        var fixtureFiles = Directory.GetFiles(fixturesDir, "*.json", SearchOption.TopDirectoryOnly);

        var theoryData = new TheoryData<string>();
        foreach (var fileName in fixtureFiles.Select(Path.GetFileName).Where(name => name is not null).OrderBy(name => name)) {
            var filePath = Path.Combine(fixturesDir, fileName!);
            if (IsMultiRoomFixture(filePath)) {
                theoryData.Add(fileName!);
            }
        }

        return theoryData;
    }

    /// <summary>
    /// Detects if a fixture is multi-room format by checking for "rooms" property.
    /// </summary>
    private static bool IsMultiRoomFixture(string filePath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(filePath));
        var isMultiRoom = doc.RootElement.TryGetProperty("rooms", out _);
        return isMultiRoom;
    }

    /// <summary>
    /// Runs a parity test for the given multi-room fixture, comparing .NET Engine output with Node.js engine output.
    /// Tests cross-room operations including:
    /// - Terminal.send (cross-room resource transfers)
    /// - PowerCreep.create, rename, delete, suicide, spawn, upgrade (global power creep management)
    /// Test passes if both engines produce identical mutations. Test fails if divergences are detected.
    /// </summary>
    /// <param name="fixtureName">JSON fixture filename (e.g., "terminal_send.json", "powercreep_create.json")</param>
    [Theory]
    [MemberData(nameof(AllMultiRoomFixtures))]
    public async Task MultiRoomFixture_MatchesNodeJsEngine(string fixtureName)
    {
        var fixturePath = ParityFixturePaths.GetFixturePath(fixtureName);
        var globalState = await MultiRoomFixtureLoader.LoadFromFileAsync(fixturePath, TestContext.Current.CancellationToken);

        var dotnetOutput = await DotNetMultiRoomParityTestRunner.RunAsync(globalState, TestContext.Current.CancellationToken);
        var nodejsOutput = await NodeJsParityTestRunner.RunFixtureAsync(fixturePath, prerequisites.HarnessDirectory, MongoFixture.ConnectionString, TestContext.Current.CancellationToken);

        var comparison = Comparison.ParityComparator.CompareMultiRoom(dotnetOutput, nodejsOutput);
        if (comparison.HasDivergences) {
            Assert.Fail(Comparison.DivergenceReporter.FormatReport(comparison, fixtureName));
        }
    }
}
