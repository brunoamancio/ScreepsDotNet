namespace ScreepsDotNet.Backend.Core.Models.Strongholds;

/// <summary>
/// Metadata describing a spawnable NPC Stronghold configuration.
/// </summary>
public sealed record StrongholdTemplate(
    string Name,
    string Description,
    int Level,
    int StructureCount);
