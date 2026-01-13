using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using ScreepsDotNet.Driver.Abstractions.Pathfinding;

namespace ScreepsDotNet.Driver.Services.Pathfinding;

internal static class PathfinderNative
{
    private const string LibraryBaseName = "libscreepspathfinder";
    private const int CostMatrixSize = 2500;

    private static readonly Lock SyncRoot = new();
    private static bool _loadAttempted;
    private static bool _available;
    private static IntPtr _libraryHandle;

    private static LoadTerrainDelegate? _loadTerrain;
    private static SearchDelegate? _search;
    private static FreeResultDelegate? _freeResult;
    private static SetRoomCallbackDelegate? _setRoomCallback;

    private static readonly RoomCallbackNative RoomCallbackThunk = HandleRoomCallback;
    private static readonly AsyncLocal<RoomCallbackContext?> RoomCallbackState = new();

    private static readonly Encoding Utf8 = Encoding.UTF8;

    public static bool IsAvailable => _available;

    public static bool TryInitialize(ILogger? logger)
    {
        if (_loadAttempted)
            return _available;

        lock (SyncRoot)
        {
            if (_loadAttempted)
                return _available;

            _loadAttempted = true;

            var rid = RuntimeInformation.RuntimeIdentifier ?? string.Empty;
            var fileName = GetLibraryFileName();
            foreach (var candidate in EnumerateCandidatePaths(fileName, rid))
            {
                try
                {
                    if (!NativeLibrary.TryLoad(candidate, out var handle))
                        continue;

                    _libraryHandle = handle;
                    _loadTerrain = GetDelegate<LoadTerrainDelegate>(handle, "ScreepsPathfinder_LoadTerrain");
                    _search = GetDelegate<SearchDelegate>(handle, "ScreepsPathfinder_Search");
                    _freeResult = GetDelegate<FreeResultDelegate>(handle, "ScreepsPathfinder_FreeResult");
                    _setRoomCallback = GetDelegate<SetRoomCallbackDelegate>(handle, "ScreepsPathfinder_SetRoomCallback");
                    _available = _loadTerrain is not null && _search is not null && _freeResult is not null;
                    if (_available)
                    {
                        _setRoomCallback?.Invoke(RoomCallbackThunk, IntPtr.Zero);
                        logger?.LogInformation("Loaded native pathfinder library from {Path}.", candidate);
                        return true;
                    }

                    logger?.LogWarning("Native pathfinder found at {Path}, but exports could not be resolved.", candidate);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Failed to load native pathfinder from {Path}.", candidate);
                }
            }

            logger?.LogWarning("Native pathfinder library not found for RID {Rid}. Using managed fallback.", rid);
            return false;
        }
    }

    public static void LoadTerrain(IReadOnlyCollection<TerrainRoomData> rooms)
    {
        if (!_available || _loadTerrain is null)
            throw new InvalidOperationException("Native pathfinder is not initialized.");

        if (rooms.Count == 0)
            throw new ArgumentException("Terrain data collection cannot be empty.", nameof(rooms));

        var handles = new List<GCHandle>();
        var terrainRooms = new ScreepsTerrainRoom[rooms.Count];
        try
        {
            var index = 0;
            foreach (var room in rooms)
            {
                if (room.TerrainBytes is not { Length: > 0 } bytes)
                    continue;
                var roomNameBytes = Utf8.GetBytes(room.RoomName + "\0");
                var roomHandle = GCHandle.Alloc(roomNameBytes, GCHandleType.Pinned);
                handles.Add(roomHandle);
                var terrainHandle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                handles.Add(terrainHandle);

                terrainRooms[index++] = new ScreepsTerrainRoom
                {
                    RoomName = roomHandle.AddrOfPinnedObject(),
                    TerrainBytes = terrainHandle.AddrOfPinnedObject(),
                    TerrainLength = bytes.Length
                };
            }

            var populated = index;
            if (populated == 0)
                throw new InvalidOperationException("No valid terrain rooms were provided to the native pathfinder.");

            if (populated < terrainRooms.Length)
                Array.Resize(ref terrainRooms, populated);

            using var pinnedRooms = new PinnedArray<ScreepsTerrainRoom>(terrainRooms);
            var result = _loadTerrain(pinnedRooms.Pointer, terrainRooms.Length);
            if (result != 0)
                throw new InvalidOperationException($"Native terrain load failed with error code {result}.");
        }
        finally
        {
            foreach (var handle in handles)
            {
                if (handle.IsAllocated)
                    handle.Free();
            }
        }
    }

    public static PathfinderResult Search(RoomPosition origin, IReadOnlyList<PathfinderGoal> goals, PathfinderOptions options)
    {
        if (!_available || _search is null || _freeResult is null)
            throw new InvalidOperationException("Native pathfinder is not initialized.");

        ArgumentNullException.ThrowIfNull(goals);
        if (goals.Count == 0)
            throw new ArgumentException("At least one goal must be provided.", nameof(goals));
        ArgumentNullException.ThrowIfNull(options);

        var nativeOrigin = new ScreepsPathfinderPoint
        {
            X = origin.X,
            Y = origin.Y,
            RoomName = origin.RoomName
        };

        var optionsNative = new ScreepsPathfinderOptionsNative
        {
            Flee = options.Flee,
            MaxRooms = Math.Clamp(options.MaxRooms, 1, 64),
            MaxOps = Math.Max(options.MaxOps, 1),
            MaxCost = options.MaxCost is { } maxCost and > 0 ? maxCost : int.MaxValue,
            PlainCost = Math.Max(options.PlainCost, 1),
            SwampCost = Math.Max(options.SwampCost, 1),
            HeuristicWeight = Math.Clamp(options.HeuristicWeight, 1.0, 9.0)
        };

        using var callbackScope = RoomCallbackScope.Enter(options.RoomCallback);
        using var goalBuffer = ConvertGoals(goals);
        var nativeResult = new ScreepsPathfinderResultNative();
        var code = _search(ref nativeOrigin, goalBuffer.Pointer, goalBuffer.Count, ref optionsNative, ref nativeResult);
        if (code != 0)
            throw new InvalidOperationException($"Native pathfinder search failed with error code {code}.");

        try
        {
            var path = ConvertPath(nativeResult);
            return new PathfinderResult(path, nativeResult.Operations, nativeResult.Cost, nativeResult.Incomplete);
        }
        finally
        {
            _freeResult(ref nativeResult);
        }
    }

    private static IReadOnlyList<RoomPosition> ConvertPath(ScreepsPathfinderResultNative result)
    {
        if (result.Path == IntPtr.Zero || result.PathLength <= 0)
            return [];

        var size = Marshal.SizeOf<ScreepsPathfinderPoint>();
        var path = new RoomPosition[result.PathLength];
        for (var i = 0; i < result.PathLength; i++)
        {
            var ptr = result.Path + i * size;
            var point = Marshal.PtrToStructure<ScreepsPathfinderPoint>(ptr);
            path[i] = new RoomPosition(point.X, point.Y, point.RoomName ?? string.Empty);
        }

        return path;
    }

    private static string GetLibraryFileName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return LibraryBaseName + ".dll";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return LibraryBaseName + ".dylib";
        return LibraryBaseName + ".so";
    }

    private static IEnumerable<string> EnumerateCandidatePaths(string fileName, string rid)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in Enumerate())
        {
            var full = Path.GetFullPath(candidate);
            if (seen.Add(full))
                yield return full;
        }

        IEnumerable<string> Enumerate()
        {
            var baseDir = AppContext.BaseDirectory ?? Environment.CurrentDirectory;
            foreach (var candidate in BuildCandidates(baseDir))
                yield return candidate;

            var directory = baseDir;
            for (var i = 0; i < 5; i++)
            {
                var parent = Directory.GetParent(directory)?.FullName;
                if (string.IsNullOrEmpty(parent))
                    break;
                directory = parent;
                foreach (var candidate in BuildCandidates(directory))
                    yield return candidate;
            }
        }

        IEnumerable<string> BuildCandidates(string root)
        {
            var direct = Path.Combine(root, fileName);
            if (!string.IsNullOrWhiteSpace(direct))
                yield return direct;

            if (!string.IsNullOrWhiteSpace(rid))
            {
                yield return Path.Combine(root, "runtimes", rid, "native", fileName);
                yield return Path.Combine(root, "src", "ScreepsDotNet.Driver", "runtimes", rid, "native", fileName);
                yield return Path.Combine(root, "ScreepsDotNet.Driver", "runtimes", rid, "native", fileName);
            }
        }
    }

    private static T GetDelegate<T>(IntPtr handle, string export) where T : Delegate
    {
        var ptr = NativeLibrary.GetExport(handle, export);
        return Marshal.GetDelegateForFunctionPointer<T>(ptr);
    }

    private static GoalBuffer ConvertGoals(IReadOnlyList<PathfinderGoal> goals)
        => GoalBuffer.Create(goals);

    private static bool HandleRoomCallback(
        byte roomX,
        byte roomY,
        out IntPtr costMatrix,
        out int costMatrixLength,
        out bool blockRoom,
        IntPtr _)
    {
        var context = RoomCallbackState.Value;
        if (context?.Callback is null)
        {
            costMatrix = IntPtr.Zero;
            costMatrixLength = 0;
            blockRoom = false;
            return true;
        }

        PathfinderRoomCallbackResult? result;
        try
        {
            var roomName = FormatRoomName(roomX, roomY);
            result = context.Callback(roomName);
        }
        catch
        {
            costMatrix = IntPtr.Zero;
            costMatrixLength = 0;
            blockRoom = false;
            return false;
        }

        if (result is null)
        {
            costMatrix = IntPtr.Zero;
            costMatrixLength = 0;
            blockRoom = false;
            return true;
        }

        if (result.BlockRoom)
        {
            costMatrix = IntPtr.Zero;
            costMatrixLength = 0;
            blockRoom = true;
            return true;
        }

        blockRoom = false;
        if (result.CostMatrix is not { Length: CostMatrixSize } matrix)
            throw new InvalidOperationException($"Cost matrix returned by roomCallback must contain exactly {CostMatrixSize} entries.");

        var handle = GCHandle.Alloc(matrix, GCHandleType.Pinned);
        context.Handles.Add(handle);
        costMatrix = handle.AddrOfPinnedObject();
        costMatrixLength = matrix.Length;
        return true;
    }

    private static string FormatRoomName(byte roomX, byte roomY)
    {
        var horizontalAxis = roomX <= 127 ? 'W' : 'E';
        var horizontalValue = roomX <= 127 ? 127 - roomX : roomX - 128;
        var verticalAxis = roomY <= 127 ? 'N' : 'S';
        var verticalValue = roomY <= 127 ? 127 - roomY : roomY - 128;
        return $"{horizontalAxis}{horizontalValue}{verticalAxis}{verticalValue}";
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int LoadTerrainDelegate(IntPtr rooms, int count);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SearchDelegate(
        ref ScreepsPathfinderPoint origin,
        IntPtr goals,
        int goalCount,
        ref ScreepsPathfinderOptionsNative options,
        ref ScreepsPathfinderResultNative result);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void FreeResultDelegate(ref ScreepsPathfinderResultNative result);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SetRoomCallbackDelegate(RoomCallbackNative? callback, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool RoomCallbackNative(
        byte roomX,
        byte roomY,
        out IntPtr costMatrix,
        out int costMatrixLength,
        [MarshalAs(UnmanagedType.I1)] out bool blockRoom,
        IntPtr userData);

    [StructLayout(LayoutKind.Sequential)]
    private struct ScreepsTerrainRoom
    {
        public IntPtr RoomName;
        public IntPtr TerrainBytes;
        public int TerrainLength;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct ScreepsPathfinderPoint
    {
        public int X;
        public int Y;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
        public string RoomName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ScreepsPathfinderGoal
    {
        public int TargetX;
        public int TargetY;
        public IntPtr RoomName;
        public int Range;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ScreepsPathfinderOptionsNative
    {
        [MarshalAs(UnmanagedType.I1)]
        public bool Flee;
        public int MaxRooms;
        public int MaxOps;
        public int MaxCost;
        public int PlainCost;
        public int SwampCost;
        public double HeuristicWeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ScreepsPathfinderResultNative
    {
        public IntPtr Path;
        public int PathLength;
        public int Operations;
        public int Cost;
        [MarshalAs(UnmanagedType.I1)]
        public bool Incomplete;
    }

    private sealed class RoomCallbackScope : IDisposable
    {
        private readonly RoomCallbackContext? _previous;
        private readonly RoomCallbackContext? _current;

        private RoomCallbackScope(PathfinderRoomCallback? callback)
        {
            _previous = RoomCallbackState.Value;
            if (callback is null)
                return;

            _current = new RoomCallbackContext(callback);
            RoomCallbackState.Value = _current;
        }

        public static RoomCallbackScope Enter(PathfinderRoomCallback? callback) => new(callback);

        public void Dispose()
        {
            _current?.Dispose();
            RoomCallbackState.Value = _previous;
        }
    }

    private sealed class RoomCallbackContext(PathfinderRoomCallback callback) : IDisposable
    {
        public PathfinderRoomCallback Callback { get; } = callback;
        public List<GCHandle> Handles { get; } = [];

        public void Dispose()
        {
            foreach (var handle in Handles)
            {
                if (handle.IsAllocated)
                    handle.Free();
            }

            Handles.Clear();
        }
    }

    private sealed class PinnedArray<T> : IDisposable where T : struct
    {
        private readonly GCHandle _handle;
        public PinnedArray(T[] array)
        {
            ArgumentNullException.ThrowIfNull(array);
            _handle = GCHandle.Alloc(array, GCHandleType.Pinned);
        }

        public IntPtr Pointer => _handle.AddrOfPinnedObject();

        public void Dispose()
        {
            if (_handle.IsAllocated)
                _handle.Free();
        }
    }

    private sealed class GoalBuffer : IDisposable
    {
        private readonly List<GCHandle> _handles;
        private readonly PinnedArray<ScreepsPathfinderGoal>? _pinnedGoals;

        private GoalBuffer(ScreepsPathfinderGoal[] goals, List<GCHandle> handles)
        {
            _handles = handles;
            _pinnedGoals = goals.Length > 0 ? new PinnedArray<ScreepsPathfinderGoal>(goals) : null;
            Count = goals.Length;
        }

        public static GoalBuffer Create(IReadOnlyList<PathfinderGoal> goals)
        {
            var nativeGoals = new ScreepsPathfinderGoal[goals.Count];
            var handles = new List<GCHandle>(goals.Count);
            for (var i = 0; i < goals.Count; i++)
            {
                var goal = goals[i] ?? throw new ArgumentException("Goal entries cannot be null.", nameof(goals));
                var roomBytes = Utf8.GetBytes(goal.Target.RoomName + "\0");
                var handle = GCHandle.Alloc(roomBytes, GCHandleType.Pinned);
                handles.Add(handle);

                nativeGoals[i] = new ScreepsPathfinderGoal
                {
                    TargetX = goal.Target.X,
                    TargetY = goal.Target.Y,
                    RoomName = handle.AddrOfPinnedObject(),
                    Range = Math.Max(goal.Range ?? 0, 0)
                };
            }

            return new GoalBuffer(nativeGoals, handles);
        }

        public IntPtr Pointer => _pinnedGoals?.Pointer ?? IntPtr.Zero;
        public int Count { get; }

        public void Dispose()
        {
            _pinnedGoals?.Dispose();
            foreach (var handle in _handles)
            {
                if (handle.IsAllocated)
                    handle.Free();
            }
        }
    }
}
