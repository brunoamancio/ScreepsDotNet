using System.Text;
using Microsoft.Extensions.Logging;
using ScreepsDotNet.Driver.Abstractions.Pathfinding;

namespace ScreepsDotNet.Driver.Services.Pathfinding;

internal sealed class PathfinderService(ILogger<PathfinderService>? logger = null) : IPathfinderService
{
    private const int RoomSize = 50;
    private const int RoomArea = RoomSize * RoomSize;
    private const int PackedTerrainBytes = RoomArea / 4;
    private static readonly Encoding Utf8 = Encoding.UTF8;
    private readonly ILogger<PathfinderService>? _logger = logger;
    private volatile bool _initialized;
    private volatile bool _nativeReady;

    public Task InitializeAsync(IEnumerable<TerrainRoomData> terrainData, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(terrainData);

        var nativeRooms = new List<TerrainRoomData>();

        foreach (var room in terrainData)
        {
            token.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(room.RoomName) || room.TerrainBytes is not { Length: > 0 })
                continue;

            var packed = TryPackTerrain(room.TerrainBytes);
            if (packed is null)
            {
                _logger?.LogWarning("Room {Room} terrain could not be converted for the native pathfinder.", room.RoomName);
                continue;
            }

            nativeRooms.Add(new TerrainRoomData(room.RoomName, packed));
        }

        if (nativeRooms.Count == 0)
            throw new InvalidOperationException("Native pathfinder requires at least one valid terrain room.");

        if (!PathfinderNative.TryInitialize(_logger))
            throw new InvalidOperationException("Native pathfinder library not found. Ensure native binaries are downloaded before initialization.");

        try
        {
            PathfinderNative.LoadTerrain(nativeRooms);
            _nativeReady = true;
            _logger?.LogInformation("Native pathfinder initialized with {Count} rooms.", nativeRooms.Count);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Native pathfinder initialization failed.", ex);
        }

        _initialized = true;
        _logger?.LogInformation("Pathfinder initialized with {Count} rooms.", nativeRooms.Count);
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

        if (!_nativeReady)
            throw new InvalidOperationException("Native pathfinder must be initialized before searching.");

        return PathfinderNative.Search(origin, goals, options);
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
}
