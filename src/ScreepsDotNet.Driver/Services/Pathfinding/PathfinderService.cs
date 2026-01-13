using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScreepsDotNet.Driver.Abstractions.Pathfinding;

namespace ScreepsDotNet.Driver.Services.Pathfinding;

internal sealed class PathfinderService(
    ILogger<PathfinderService>? logger = null,
    IOptions<PathfinderServiceOptions>? serviceOptions = null) : IPathfinderService
{
    private const int RoomSize = 50;
    private const int RoomArea = RoomSize * RoomSize;
    private const int PackedTerrainBytes = RoomArea / 4;
    private static readonly Encoding Utf8 = Encoding.UTF8;
    private readonly ILogger<PathfinderService>? _logger = logger;
    private readonly bool _nativeFeatureEnabled = serviceOptions?.Value.EnableNative ?? true;
    private readonly ConcurrentDictionary<string, TerrainGrid> _terrain = new(StringComparer.OrdinalIgnoreCase);
    private volatile bool _initialized;
    private volatile bool _nativeReady;

    public Task InitializeAsync(IEnumerable<TerrainRoomData> terrainData, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(terrainData);

        var nativeRooms = _nativeFeatureEnabled ? new List<TerrainRoomData>() : null;
        var loaded = 0;

        foreach (var room in terrainData)
        {
            token.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(room.RoomName) || room.TerrainBytes is not { Length: > 0 })
                continue;

            var grid = TerrainGrid.TryCreate(room.RoomName, room.TerrainBytes);
            if (grid is null)
            {
                _logger?.LogWarning("Unable to parse terrain data for room {Room}.", room.RoomName);
                continue;
            }

            _terrain[room.RoomName] = grid;
            loaded++;

            if (nativeRooms is null)
                continue;

            var packed = TryPackTerrain(room.TerrainBytes);
            if (packed is null)
            {
                _logger?.LogWarning("Room {Room} terrain could not be converted for the native pathfinder.", room.RoomName);
                continue;
            }

            nativeRooms.Add(new TerrainRoomData(room.RoomName, packed));
        }

        if (_nativeFeatureEnabled)
        {
            if (nativeRooms is not { Count: > 0 })
                throw new InvalidOperationException("Native pathfinder enabled but no terrain rooms were provided.");

            if (!PathfinderNative.TryInitialize(_logger))
                throw new InvalidOperationException("Native pathfinder library not found. Ensure native binaries are downloaded or disable EnableNative.");

            try
            {
                PathfinderNative.LoadTerrain(nativeRooms);
                _nativeReady = true;
                _logger?.LogInformation("Native pathfinder initialized with {Count} rooms.", nativeRooms.Count);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Native pathfinder initialization failed. Disable EnableNative to fall back to the managed solver.", ex);
            }
        }
        else
            _logger?.LogInformation("Native pathfinder disabled via configuration; using managed fallback.");

        _initialized = true;
        _logger?.LogInformation("Pathfinder initialized with {Count} rooms.", loaded);
        return Task.CompletedTask;
    }

    public PathfinderResult Search(RoomPosition origin, PathfinderGoal goal, PathfinderOptions options)
        => Search(origin, [goal], options);

    public PathfinderResult Search(RoomPosition origin, IReadOnlyList<PathfinderGoal> goals, PathfinderOptions options)
    {
        ArgumentNullException.ThrowIfNull(goals);
        if (goals.Count == 0)
            throw new ArgumentException("At least one goal must be provided.", nameof(goals));
        ArgumentNullException.ThrowIfNull(options);

        if (!_initialized)
            throw new InvalidOperationException("InitializeAsync must be called before Search.");

        if (_nativeFeatureEnabled)
        {
            if (!_nativeReady)
                throw new InvalidOperationException("Native pathfinder must be initialized before searching.");
            return PathfinderNative.Search(origin, goals, options);
        }

        return ManagedSearch(origin, goals, options);
    }

    private PathfinderResult ManagedSearch(RoomPosition origin, IReadOnlyList<PathfinderGoal> goals, PathfinderOptions options)
    {
        var primaryGoal = goals[0];
        if (goals.Count > 1)
            _logger?.LogWarning("Managed pathfinder only considers the first goal; enable native pathfinder for multi-goal searches.");

        if (!string.Equals(origin.RoomName, primaryGoal.Target.RoomName, StringComparison.OrdinalIgnoreCase))
        {
            _logger?.LogWarning("Managed pathfinder cannot process multi-room paths. Origin {OriginRoom}, Target {TargetRoom}. Enable native pathfinder for full support.",
                origin.RoomName, primaryGoal.Target.RoomName);
            return CreateIncompleteResult();
        }

        if (!_terrain.TryGetValue(origin.RoomName, out var grid))
            throw new InvalidOperationException($"Terrain data for room '{origin.RoomName}' is not loaded.");

        var callbackContext = EvaluateRoomCallback(options, origin.RoomName);
        if (callbackContext.Blocked)
            return CreateIncompleteResult();

        return AStar(grid, origin, primaryGoal, options, callbackContext.CostMatrix);
    }

    private static byte[]? TryPackTerrain(byte[] data)
    {
        if (data.Length == PackedTerrainBytes)
            return data;

        var codes = TryExtractTerrainCodes(data);
        if (codes is null)
            return null;

        var packed = new byte[PackedTerrainBytes];
        for (var x = 0; x < RoomSize; x++)
        {
            for (var y = 0; y < RoomSize; y++)
            {
                var terrainIndex = y * RoomSize + x;
                var value = (byte)(codes[terrainIndex] & 0x03);
                var ii = x * RoomSize + y;
                var bucket = ii / 4;
                var shift = (ii % 4) * 2;
                var mask = (byte)(0x03 << shift);
                packed[bucket] = (byte)((packed[bucket] & ~mask) | (value << shift));
            }
        }

        return packed;
    }

    private static byte[]? TryExtractTerrainCodes(byte[] data)
    {
        if (data.Length == PackedTerrainBytes)
            return UnpackPackedTerrain(data);

        if (data.Length == RoomArea)
        {
            var codes = new byte[RoomArea];
            for (var i = 0; i < RoomArea; i++)
                codes[i] = NormalizeCode(data[i]);
            return codes;
        }

        var text = Utf8.GetString(data);
        if (text.Length != RoomArea)
            return null;

        var buffer = new byte[RoomArea];
        for (var i = 0; i < text.Length; i++)
            buffer[i] = NormalizeCode((byte)text[i]);
        return buffer;
    }

    private static byte[] UnpackPackedTerrain(byte[] packed)
    {
        var codes = new byte[RoomArea];
        for (var x = 0; x < RoomSize; x++)
        {
            for (var y = 0; y < RoomSize; y++)
            {
                var ii = x * RoomSize + y;
                var bucket = ii / 4;
                var shift = (ii % 4) * 2;
                var value = (byte)((packed[bucket] >> shift) & 0x03);
                var terrainIndex = y * RoomSize + x;
                codes[terrainIndex] = value;
            }
        }

        return codes;
    }

    private static byte NormalizeCode(byte value) =>
        value switch
        {
            (byte)'0' or 0 => 0,
            (byte)'1' or 1 => 1,
            (byte)'2' or 2 => 2,
            (byte)'3' or 3 => 3,
            _ => (byte)(value & 0x03)
        };

    private static byte[]? NormalizeForGrid(byte[] data)
    {
        if (data.Length == RoomArea)
            return data;

        if (data.Length == PackedTerrainBytes)
            return UnpackPackedTerrain(data);

        var text = Utf8.GetString(data);
        if (text.Length != RoomArea)
            return null;

        var buffer = new byte[RoomArea];
        for (var i = 0; i < text.Length; i++)
            buffer[i] = (byte)text[i];
        return buffer;
    }

    private static CallbackContext EvaluateRoomCallback(PathfinderOptions options, string roomName)
    {
        if (options.RoomCallback is null)
            return CallbackContext.None;

        PathfinderRoomCallbackResult? result;
        try
        {
            result = options.RoomCallback(roomName);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"roomCallback for room '{roomName}' threw an exception.", ex);
        }

        if (result is null)
            return CallbackContext.None;

        if (result.BlockRoom)
            return CallbackContext.BlockedRoom;

        if (result.CostMatrix is null)
            return CallbackContext.None;

        if (result.CostMatrix.Length != RoomArea)
            throw new InvalidOperationException($"roomCallback for room '{roomName}' must return a cost matrix with exactly {RoomArea} entries.");

        return new CallbackContext(false, result.CostMatrix);
    }

    private static PathfinderResult AStar(TerrainGrid grid, RoomPosition origin, PathfinderGoal goal, PathfinderOptions options, byte[]? costMatrix)
    {
        var start = ToIndex(origin);
        var startNode = new Node(origin.X, origin.Y, start);
        var targetRange = Math.Max(0, goal.Range ?? 0);
        if (options.Flee && targetRange == 0)
            targetRange = 1;
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

            var currentDistance = Range(current.X, current.Y, goal.Target);
            if (!options.Flee)
            {
                if (currentDistance <= targetRange)
                {
                    targetReached = true;
                    break;
                }
            }
            else if (currentDistance >= targetRange)
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
                if (costMatrix is { } matrix)
                {
                    var overrideCost = matrix[nextIndex];
                    if (overrideCost >= byte.MaxValue)
                        continue;
                    moveCost += overrideCost;
                }

                var tentative = visitedCost[current.Index] + moveCost;
                if (tentative >= visitedCost[nextIndex])
                    continue;

                visitedCost[nextIndex] = tentative;
                cameFrom[nextIndex] = current.Index;
                var nextDistance = Range(nextX, nextY, goal.Target);
                var heuristic = options.Flee
                    ? Math.Max(0, targetRange - nextDistance)
                    : nextDistance;
                var priority = tentative + heuristic;
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
        => new([], Operations: 0, Cost: 0, Incomplete: true);

    private static bool IsInside(int x, int y) => x is >= 0 and < RoomSize && y is >= 0 and < RoomSize;

    private static int Range(int x, int y, RoomPosition target)
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

    private readonly record struct CallbackContext(bool Blocked, byte[]? CostMatrix)
    {
        public static CallbackContext None { get; } = new(false, null);
        public static CallbackContext BlockedRoom { get; } = new(true, null);
    }

    private sealed class TerrainGrid
    {
        private readonly TerrainCell[] _cells;

        private TerrainGrid(TerrainCell[] cells)
            => _cells = cells;

        public TerrainCell this[int x, int y] => _cells[Index(x, y)];

        public static TerrainGrid? TryCreate(string roomName, byte[] data)
        {
            var normalized = NormalizeForGrid(data);
            return normalized is null ? null : new TerrainGrid(DecodeSpan(normalized));
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
