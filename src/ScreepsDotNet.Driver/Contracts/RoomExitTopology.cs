namespace ScreepsDotNet.Driver.Contracts;

/// <summary>
/// Describes the number of walkable edge tiles per direction plus the neighbor metadata.
/// </summary>
public sealed record RoomExitTopology(
    RoomExitDescriptor? Top,
    RoomExitDescriptor? Right,
    RoomExitDescriptor? Bottom,
    RoomExitDescriptor? Left);

/// <summary>
/// Details for a single exit direction (target room, exit tiles, accessibility).
/// </summary>
public sealed record RoomExitDescriptor(
    string TargetRoomName,
    int ExitCount,
    bool TargetAccessible);
