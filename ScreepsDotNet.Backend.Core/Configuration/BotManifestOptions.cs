namespace ScreepsDotNet.Backend.Core.Configuration;

/// <summary>
/// Configuration describing where to load legacy bot definitions from.
/// </summary>
public sealed class BotManifestOptions
{
    /// <summary>
    /// Path to the legacy mods manifest (mods.json) that lists bot AI directories.
    /// </summary>
    public string? ManifestFile { get; set; }
}
