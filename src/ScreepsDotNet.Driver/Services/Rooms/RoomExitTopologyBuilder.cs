namespace ScreepsDotNet.Driver.Services.Rooms;

using System.Collections.Generic;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Utilities;
using ScreepsDotNet.Driver.Contracts;

internal static class RoomExitTopologyBuilder
{
    private const int RoomSize = 50;

    public static IReadOnlyDictionary<string, RoomExitTopology> Build(
        IReadOnlyCollection<string> roomNames,
        IReadOnlyDictionary<string, string> terrainByRoom)
    {
        if (roomNames.Count == 0)
            return new Dictionary<string, RoomExitTopology>(0, StringComparer.Ordinal);

        var accessible = new HashSet<string>(roomNames, StringComparer.Ordinal);
        var result = new Dictionary<string, RoomExitTopology>(roomNames.Count, StringComparer.Ordinal);

        foreach (var roomName in roomNames) {
            if (!terrainByRoom.TryGetValue(roomName, out var terrain) || string.IsNullOrEmpty(terrain))
                continue;

            if (!RoomCoordinateHelper.TryParse(roomName, out var x, out var y))
                continue;

            var top = BuildDescriptor(RoomEdge.Top, x, y, terrain, accessible);
            var right = BuildDescriptor(RoomEdge.Right, x, y, terrain, accessible);
            var bottom = BuildDescriptor(RoomEdge.Bottom, x, y, terrain, accessible);
            var left = BuildDescriptor(RoomEdge.Left, x, y, terrain, accessible);

            if (top is null && right is null && bottom is null && left is null)
                continue;

            result[roomName] = new RoomExitTopology(top, right, bottom, left);
        }

        return result;
    }

    private static RoomExitDescriptor? BuildDescriptor(
        RoomEdge edge,
        int roomX,
        int roomY,
        string terrain,
        HashSet<string> accessible)
    {
        var exitCount = CountEdgeExits(edge, terrain);
        if (exitCount == 0)
            return null;

        var (targetX, targetY) = edge switch
        {
            RoomEdge.Top => (roomX, roomY - 1),
            RoomEdge.Right => (roomX + 1, roomY),
            RoomEdge.Bottom => (roomX, roomY + 1),
            RoomEdge.Left => (roomX - 1, roomY),
            _ => (roomX, roomY)
        };

        var targetRoom = RoomCoordinateHelper.ToRoomName(targetX, targetY);
        var targetAccessible = accessible.Contains(targetRoom);
        return new RoomExitDescriptor(targetRoom, exitCount, targetAccessible);
    }

    private static int CountEdgeExits(RoomEdge edge, string terrain)
    {
        var count = 0;
        switch (edge) {
            case RoomEdge.Top:
                for (var x = 0; x < RoomSize; x++) {
                    if (IsWalkable(terrain, x, 0))
                        count++;
                }
                break;
            case RoomEdge.Right:
                for (var y = 0; y < RoomSize; y++) {
                    if (IsWalkable(terrain, RoomSize - 1, y))
                        count++;
                }
                break;
            case RoomEdge.Bottom:
                for (var x = 0; x < RoomSize; x++) {
                    if (IsWalkable(terrain, x, RoomSize - 1))
                        count++;
                }
                break;
            case RoomEdge.Left:
                for (var y = 0; y < RoomSize; y++) {
                    if (IsWalkable(terrain, 0, y))
                        count++;
                }
                break;
            default:
                break;
        }

        return count;
    }

    private static bool IsWalkable(string terrain, int x, int y)
    {
        var index = (y * RoomSize) + x;
        if ((uint)index >= (uint)terrain.Length)
            return false;

        var mask = TerrainEncoding.Decode(terrain[index]);
        return (mask & ScreepsGameConstants.TerrainMaskWall) == 0;
    }

    private enum RoomEdge
    {
        Top,
        Right,
        Bottom,
        Left
    }
}
