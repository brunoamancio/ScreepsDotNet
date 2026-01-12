using ScreepsDotNet.Backend.Core.Constants;

namespace ScreepsDotNet.Backend.Core.Models.Strongholds;

/// <summary>
/// Describes a single structure placement within a stronghold template.
/// </summary>
public sealed record StrongholdStructureBlueprint(
    StructureType Type,
    int OffsetX,
    int OffsetY,
    int? Level,
    string? Behavior);
