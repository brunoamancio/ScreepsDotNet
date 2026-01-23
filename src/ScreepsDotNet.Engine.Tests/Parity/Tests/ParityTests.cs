namespace ScreepsDotNet.Engine.Tests.Parity.Tests;

using ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

/// <summary>
/// TRUE parity tests comparing .NET Engine with Node.js engine field-by-field.
/// All tests follow the same pattern: load fixture, run both engines, compare outputs.
/// New tests are automatically discovered from JSON fixtures in the Fixtures directory.
/// </summary>
[Trait("Category", "Parity")]
[Collection(nameof(Integration.MongoDbParityCollection))]
public sealed class ParityTests(Integration.MongoDbParityFixture mongoFixture, Integration.ParityTestPrerequisites prerequisites)
{
    /// <summary>
    /// Auto-discovers all JSON fixture files in the Parity/Fixtures directory.
    /// New fixtures are automatically included without code changes.
    /// </summary>
    public static TheoryData<string> AllFixtures()
    {
        var fixturesDir = Path.Combine("Parity", "Fixtures");
        var fixtureFiles = Directory.GetFiles(fixturesDir, "*.json", SearchOption.TopDirectoryOnly);

        var theoryData = new TheoryData<string>();
        foreach (var fileName in fixtureFiles.Select(Path.GetFileName).Where(name => name is not null).OrderBy(name => name)) {
            theoryData.Add(fileName!);
        }

        return theoryData;
    }

    /// <summary>
    /// Runs a parity test for the given fixture, comparing .NET Engine output with Node.js engine output.
    /// Test passes if both engines produce identical mutations. Test fails if divergences are detected.
    /// </summary>
    /// <param name="fixtureName">JSON fixture filename (e.g., "harvest_single_work.json")</param>
    [Theory]
    [MemberData(nameof(AllFixtures))]
    public async Task Fixture_MatchesNodeJsEngine(string fixtureName)
    {
        var fixturePath = ParityFixturePaths.GetFixturePath(fixtureName);
        var state = await JsonFixtureLoader.LoadFromFileAsync(fixturePath, TestContext.Current.CancellationToken);

        var dotnetOutput = await DotNetParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);
        var nodejsOutput = await NodeJsParityTestRunner.RunFixtureAsync(fixturePath, prerequisites.HarnessDirectory, mongoFixture.ConnectionString, TestContext.Current.CancellationToken);

        var comparison = Comparison.ParityComparator.Compare(dotnetOutput, nodejsOutput);
        if (comparison.HasDivergences) {
            Assert.Fail(Comparison.DivergenceReporter.FormatReport(comparison, fixtureName));
        }
    }
}
