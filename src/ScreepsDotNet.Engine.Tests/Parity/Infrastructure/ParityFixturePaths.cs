namespace ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

/// <summary>
/// Provides centralized fixture path resolution for parity tests.
/// </summary>
public static class ParityFixturePaths
{
    private const string FixturesDirectory = "Parity";
    private const string FixturesSubdirectory = "Fixtures";

    /// <summary>
    /// Gets the full path to a fixture file by name.
    /// </summary>
    /// <param name="fixtureName">The fixture filename (e.g., "keeper_combat.json")</param>
    /// <returns>The full path: "Parity/Fixtures/{fixtureName}"</returns>
    public static string GetFixturePath(string fixtureName)
        => Path.Combine(FixturesDirectory, FixturesSubdirectory, fixtureName);
}
