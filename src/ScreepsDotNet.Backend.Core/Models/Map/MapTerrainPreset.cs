namespace ScreepsDotNet.Backend.Core.Models.Map;

/// <summary>
/// High-level presets that control how procedural room terrain is generated for CLI map commands.
/// </summary>
public enum MapTerrainPreset
{
    Plain,

    /// <summary>
    /// Thin rings of swamp distributed around the room.
    /// </summary>
    SwampLow,

    /// <summary>
    /// Heavy swamp coverage with scattered walls.
    /// </summary>
    SwampHeavy,

    /// <summary>
    /// Alternating checkerboard of swamps and plains.
    /// </summary>
    Checker,

    /// <summary>
    /// Mixed preset with random swamps/walls while keeping edges blocked.
    /// </summary>
    Mixed
}
