namespace ScreepsDotNet.Backend.Core.Models.Strongholds;

/// <summary>
/// Describes a single structure placement within a stronghold template.
/// </summary>
public sealed record StrongholdStructureBlueprint(
    string Type,
    int OffsetX,
    int OffsetY,
    int? Level,
    string? Behavior);
