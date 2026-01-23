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
    /// Fixtures with known architectural differences that have dedicated test methods.
    /// These are excluded from the common parity test to avoid false failures.
    /// </summary>
    private static readonly HashSet<string> FixturesWithKnownDivergences = new(StringComparer.OrdinalIgnoreCase)
    {
        "harvest_basic.json"  // Timer representation difference (Node.js countdown vs .NET absolute timestamp)
    };

    /// <summary>
    /// Auto-discovers all JSON fixture files in the Parity/Fixtures directory.
    /// New fixtures are automatically included without code changes.
    /// Excludes fixtures with known architectural differences (see FixturesWithKnownDivergences).
    /// </summary>
    public static TheoryData<string> AllFixtures()
    {
        var fixturesDir = Path.Combine("Parity", "Fixtures");
        var fixtureFiles = Directory.GetFiles(fixturesDir, "*.json", SearchOption.TopDirectoryOnly);

        var theoryData = new TheoryData<string>();
        foreach (var fileName in fixtureFiles.Select(Path.GetFileName).Where(name => name is not null).OrderBy(name => name)) {
            if (!FixturesWithKnownDivergences.Contains(fileName!)) {
                theoryData.Add(fileName!);
            }
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

    /// <summary>
    /// ARCHITECTURAL DIFFERENCE TEST: harvest_basic.json uses different timer representation.
    /// Node.js: Uses countdown timer (ticksToRegeneration) that decrements each tick.
    /// .NET: Uses absolute timestamp (NextRegenerationTime) for efficiency (avoids unnecessary DB writes).
    /// This test validates that ONLY the timer representation differs, all other behavior is identical.
    /// Decision: Keep .NET implementation for better performance (documented in e10.md and parity-divergences.md).
    /// </summary>
    [Fact]
    public async Task HarvestBasic_HasKnownTimerDivergence_AllOtherBehaviorMatches()
    {
        var fixturePath = ParityFixturePaths.GetFixturePath("harvest_basic.json");
        var state = await JsonFixtureLoader.LoadFromFileAsync(fixturePath, TestContext.Current.CancellationToken);

        var dotnetOutput = await DotNetParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);
        var nodejsOutput = await NodeJsParityTestRunner.RunFixtureAsync(fixturePath, prerequisites.HarnessDirectory, mongoFixture.ConnectionString, TestContext.Current.CancellationToken);

        var comparison = Comparison.ParityComparator.Compare(dotnetOutput, nodejsOutput);

        // Expected divergence: Source patch exists in Node.js but not in .NET
        // Node.js decrements ticksToRegeneration (300 -> 299) every tick, .NET doesn't patch until regeneration completes
        // This is an intentional efficiency improvement - no need to write to DB every tick
        var expectedDivergence = comparison.Divergences.FirstOrDefault(d =>
            d.Path.Contains("mutations.patches[") &&
            d.Path.Contains("source") &&
            d.Message.Contains("Patch exists in Node.js but not in .NET"));

        if (expectedDivergence is not null) {
            // Remove expected divergence and check if any unexpected divergences remain
            var unexpectedDivergences = comparison.Divergences.Where(d => d != expectedDivergence).ToList();
            if (unexpectedDivergences.Count > 0) {
                Assert.Fail($"❌ harvest_basic.json has unexpected divergences beyond timer representation:\n\n{Comparison.DivergenceReporter.FormatReport(new Comparison.ParityComparisonResult(unexpectedDivergences), "harvest_basic.json")}");
            }

            // Test passes - only expected timer divergence found
            Assert.True(true, "✅ harvest_basic.json: Only expected timer representation difference found (Node.js countdown vs .NET absolute timestamp)");
        }
        else if (comparison.HasDivergences) {
            // No expected divergence, but other divergences exist - fail with details
            Assert.Fail(Comparison.DivergenceReporter.FormatReport(comparison, "harvest_basic.json"));
        }
        else {
            // Perfect match - test passes (Node.js harness might have been updated to match .NET behavior)
            Assert.True(true, "✅ harvest_basic.json: Perfect match with Node.js (no divergences)");
        }
    }
}
