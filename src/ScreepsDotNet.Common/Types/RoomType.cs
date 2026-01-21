namespace ScreepsDotNet.Common.Types;

/// <summary>
/// Room type classification based on coordinates and gameplay mechanics.
/// </summary>
public enum RoomType
{
    /// <summary>
    /// Room type cannot be determined (invalid coordinates or uninitialized).
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Standard room with controller (W/E x N/S, not center sector or highway).
    /// </summary>
    Normal = 1,

    /// <summary>
    /// Source Keeper room (center sector: x % 10 == 0 AND y % 10 == 0).
    /// Contains invader cores, strongholds, and high-capacity sources (4000 energy).
    /// </summary>
    Keeper = 2,

    /// <summary>
    /// Highway room (one coordinate is x % 10 == 0 OR y % 10 == 0, but not both).
    /// No controller, no sources, used for transit and power banks.
    /// </summary>
    Highway = 3,

    /// <summary>
    /// Respawn/novice area room (restricted access for new players).
    /// </summary>
    RespawnArea = 4
}
