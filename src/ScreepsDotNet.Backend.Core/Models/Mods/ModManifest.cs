namespace ScreepsDotNet.Backend.Core.Models.Mods;

using ScreepsDotNet.Backend.Core.Intents;

/// <summary>
/// Snapshot of the parsed mods manifest (mods.json).
/// </summary>
/// <param name="SourcePath">Absolute path to the manifest on disk, if available.</param>
/// <param name="LastModifiedUtc">Timestamp of the manifest file.</param>
/// <param name="Bots">Configured bot AI definitions (bot name -> relative directory).</param>
/// <param name="CustomIntentTypes">Additional intent definitions contributed by mods.</param>
/// <param name="CustomObjectTypes">Custom object definitions that should surface via /api/server/info.</param>
public sealed record ModManifest(
    string? SourcePath,
    DateTimeOffset LastModifiedUtc,
    IReadOnlyDictionary<string, string> Bots,
    IReadOnlyDictionary<string, IntentDefinition> CustomIntentTypes,
    IReadOnlyDictionary<string, object?> CustomObjectTypes)
{
    public static ModManifest Empty { get; } = new(null,
                                                  DateTimeOffset.MinValue,
                                                  new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                                                  new Dictionary<string, IntentDefinition>(StringComparer.Ordinal),
                                                  new Dictionary<string, object?>(StringComparer.Ordinal));
}
