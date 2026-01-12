using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;
using ScreepsDotNet.Driver.Abstractions.Pathfinding;

namespace ScreepsDotNet.Driver.Services.Pathfinding;

internal sealed class PathfinderService(ILogger<PathfinderService>? logger = null) : IPathfinderService
{
    private const int RoomSize = 50;
    private const int RoomArea = RoomSize * RoomSize;
    private readonly ConcurrentDictionary<string, TerrainGrid> _terrain = new(StringComparer.OrdinalIgnoreCase);
    private volatile bool _initialized;

    public Task InitializeAsync(IEnumerable<TerrainRoomData> terrainData, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(terrainData);

        var loaded = 0;
        foreach (var room in terrainData)
        {
            token.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(room.RoomName) || room.TerrainBytes is not { Length: > 0 })
                continue;

            var grid = TerrainGrid.TryCreate(room.RoomName, room.TerrainBytes);
            if (grid is null)
            {
                logger?.LogWarning("Unable to parse terrain data for room {Room}.", room.RoomName);
                continue;
            }

            _terrain[room.RoomName] = grid;
            loaded++;
        }

        _initialized = true;
        logger?.LogInformation("Pathfinder initialized with {Count} rooms.", loaded);
        return Task.CompletedTask;
    }

    public PathfinderResult Search(RoomPosition origin, PathfinderGoal goal, PathfinderOptions options)
    {
        if (!_initialized)
            throw new InvalidOperationException("InitializeAsync must be called before Search.");

        if (!string.Equals(origin.RoomName, goal.Target.RoomName, StringComparison.OrdinalIgnoreCase))
        {
            logger?.LogWarning("Multi-room pathfinding is not supported yet. Origin {OriginRoom}, Target {TargetRoom}.",
                origin.RoomName, goal.Target.RoomName);
            return CreateIncompleteResult();
        }

        if (!_terrain.TryGetValue(origin.RoomName, out var grid))
            throw new InvalidOperationException($"Terrain data for room '{origin.RoomName}' is not loaded.");

        var result = AStar(grid, origin, goal, options);
        return result;
    }

    private static PathfinderResult AStar(TerrainGrid grid, RoomPosition origin, PathfinderGoal goal, PathfinderOptions options)
    {
        var start = ToIndex(origin);
        var startNode = new Node(origin.X, origin.Y, start);
        var targetRange = Math.Max(0, goal.Range ?? 0);
        var visitedCost = new int[RoomArea];
        Array.Fill(visitedCost, int.MaxValue);
        visitedCost[start] = 0;

        var cameFrom = new int[RoomArea];
        Array.Fill(cameFrom, -1);

        var open = new PriorityQueue<Node, int>();
        open.Enqueue(startNode, 0);

        var operations = 0;
        var current = startNode;
        var targetReached = false;

        while (open.Count > 0 && operations < options.MaxOps)
        {
            operations++;
            current = open.Dequeue();

            if (InRange(current.X, current.Y, goal.Target, targetRange))
            {
                targetReached = true;
                break;
            }

            foreach (var (nx, ny) in NeighborOffsets)
            {
                var nextX = current.X + nx;
                var nextY = current.Y + ny;
                if (!IsInside(nextX, nextY))
                    continue;

                var terrain = grid[nextX, nextY];
                if (!terrain.Walkable)
                    continue;

                var moveCost = current.X != nextX && current.Y != nextY ? options.PlainCost : 0;
                moveCost += terrain.IsSwamp ? options.SwampCost : options.PlainCost;

                var nextIndex = Index(nextX, nextY);
                var tentative = visitedCost[current.Index] + moveCost;
                if (tentative >= visitedCost[nextIndex])
                    continue;

                visitedCost[nextIndex] = tentative;
                cameFrom[nextIndex] = current.Index;
                var priority = tentative + Heuristic(nextX, nextY, goal.Target);
                open.Enqueue(new Node(nextX, nextY, nextIndex), priority);
            }
        }

        if (!targetReached)
            return CreateIncompleteResult();

        var path = ReconstructPath(cameFrom, current.Index, start, origin.RoomName);
        return new PathfinderResult(path, operations, visitedCost[current.Index], Incomplete: false);
    }

    private static IReadOnlyList<RoomPosition> ReconstructPath(int[] cameFrom, int currentIndex, int startIndex, string roomName)
    {
        var stack = new Stack<RoomPosition>();
        var walker = currentIndex;
        while (walker != startIndex && walker >= 0)
        {
            var (x, y) = FromIndex(walker);
            stack.Push(new RoomPosition(x, y, roomName));
            walker = cameFrom[walker];
        }

        return stack.ToArray();
    }

    private static PathfinderResult CreateIncompleteResult()
        => new(Array.Empty<RoomPosition>(), Operations: 0, Cost: 0, Incomplete: true);

    private static bool InRange(int x, int y, RoomPosition target, int range)
        => Math.Max(Math.Abs(x - target.X), Math.Abs(y - target.Y)) <= range;

    private static bool IsInside(int x, int y) => x is >= 0 and < RoomSize && y is >= 0 and < RoomSize;

    private static int Heuristic(int x, int y, RoomPosition target)
        => Math.Max(Math.Abs(x - target.X), Math.Abs(y - target.Y));

    private static int Index(int x, int y) => y * RoomSize + x;

    private static (int x, int y) FromIndex(int index) => (index % RoomSize, index / RoomSize);

    private static int ToIndex(RoomPosition position) => Index(position.X, position.Y);

    private static readonly (int x, int y)[] NeighborOffsets =
    [
        (0, -1), (1, -1), (1, 0), (1, 1),
        (0, 1), (-1, 1), (-1, 0), (-1, -1)
    ];

    private readonly record struct Node(int X, int Y, int Index);

    private sealed class TerrainGrid
    {
        private readonly TerrainCell[] _cells;

        private TerrainGrid(TerrainCell[] cells)
            => _cells = cells;

        public TerrainCell this[int x, int y] => _cells[Index(x, y)];

        public static TerrainGrid? TryCreate(string roomName, byte[] data)
        {
            if (data.Length == RoomArea)
                return new TerrainGrid(DecodeSpan(data));

            var text = Encoding.UTF8.GetString(data);
            if (text.Length == RoomArea)
            {
                var temp = new byte[RoomArea];
                for (var i = 0; i < text.Length; i++)
                    temp[i] = (byte)text[i];
                return new TerrainGrid(DecodeSpan(temp));
            }

            return null;
        }

        private static TerrainCell[] DecodeSpan(IReadOnlyList<byte> source)
        {
            var cells = new TerrainCell[RoomArea];
            for (var i = 0; i < RoomArea && i < source.Count; i++)
                cells[i] = Decode(source[i]);

            return cells;
        }

        private static TerrainCell Decode(byte value)
        {
            var mask = value switch
            {
                (byte)'1' or 1 => TerrainFlags.Wall,
                (byte)'2' or 2 => TerrainFlags.Swamp,
                (byte)'3' or 3 => TerrainFlags.Wall | TerrainFlags.Swamp,
                _ => TerrainFlags.Plain
            };

            return new TerrainCell((mask & TerrainFlags.Wall) == 0, (mask & TerrainFlags.Swamp) != 0);
        }
    }

    private readonly record struct TerrainCell(bool Walkable, bool IsSwamp);

    [Flags]
    private enum TerrainFlags : byte
    {
        Plain = 0,
        Wall = 1,
        Swamp = 2
    }
}
