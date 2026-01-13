namespace ScreepsDotNet.Driver.Abstractions.Pathfinding;

public interface IPathfinderService
{
    Task InitializeAsync(IEnumerable<TerrainRoomData> terrainData, CancellationToken token = default);
    PathfinderResult Search(RoomPosition origin, PathfinderGoal goal, PathfinderOptions options);
    PathfinderResult Search(RoomPosition origin, IReadOnlyList<PathfinderGoal> goals, PathfinderOptions options);
}

public sealed record TerrainRoomData(string RoomName, byte[] TerrainBytes);

public sealed record RoomPosition(int X, int Y, string RoomName);

public sealed record PathfinderGoal(RoomPosition Target, int? Range = null);

public sealed record PathfinderOptions(
    bool Flee = false,
    int MaxRooms = 1,
    int MaxOps = 2000,
    int PlainCost = 1,
    int SwampCost = 5,
    bool IgnoreRoads = false,
    bool IgnoreDestructibleStructures = false,
    int? MaxCost = null,
    double HeuristicWeight = 1.2,
    PathfinderRoomCallback? RoomCallback = null);

public sealed record PathfinderResult(
    IReadOnlyList<RoomPosition> Path,
    int Operations,
    int Cost,
    bool Incomplete);

public delegate PathfinderRoomCallbackResult? PathfinderRoomCallback(string roomName);

public sealed record PathfinderRoomCallbackResult(byte[]? CostMatrix, bool BlockRoom = false);
