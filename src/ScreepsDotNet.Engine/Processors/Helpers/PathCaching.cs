using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;

namespace ScreepsDotNet.Engine.Processors.Helpers;

/// <summary>
/// Path caching utilities for NPC AI movement.
/// Implements Node.js-compatible position packing format (6 bits X, 6 bits Y).
/// </summary>
internal static class PathCaching
{
    private const int CoordinateBits = 6;
    private const int CharOffset = 32;
    private const int MaxCoordinate = (1 << CoordinateBits) - 1; // 63

    /// <summary>
    /// Pack a position into a single character string.
    /// Format: 6 bits for X, 6 bits for Y, offset by 32 for valid Unicode.
    /// </summary>
    public static string PackPosition(int x, int y)
    {
        if (x < 0 || x > MaxCoordinate)
            throw new ArgumentOutOfRangeException(nameof(x), x, $"X must be between 0 and {MaxCoordinate}");
        if (y < 0 || y > MaxCoordinate)
            throw new ArgumentOutOfRangeException(nameof(y), y, $"Y must be between 0 and {MaxCoordinate}");

        var packed = (x << CoordinateBits) | y;
        var charCode = CharOffset + packed;
        var result = char.ConvertFromUtf32(charCode);
        return result;
    }

    /// <summary>
    /// Unpack a position from a single character string.
    /// </summary>
    public static (int X, int Y) UnpackPosition(string packed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packed);

        if (packed.Length == 0)
            throw new ArgumentException("Packed position cannot be empty", nameof(packed));

        var charCode = char.ConvertToUtf32(packed, 0);
        var value = charCode - CharOffset;

        var x = value >> CoordinateBits;
        var y = value & MaxCoordinate;

        var result = (x, y);
        return result;
    }

    /// <summary>
    /// Serialize a path of positions into a packed string.
    /// Each position becomes a single character.
    /// </summary>
    public static string SerializePath(IReadOnlyList<(int X, int Y)> positions)
    {
        ArgumentNullException.ThrowIfNull(positions);

        if (positions.Count == 0)
            return string.Empty;

        var chars = new char[positions.Count];
        for (var i = 0; i < positions.Count; i++) {
            var (x, y) = positions[i];
            var packed = PackPosition(x, y);
            chars[i] = packed[0];
        }

        var result = new string(chars);
        return result;
    }

    /// <summary>
    /// Deserialize a packed path string into positions.
    /// </summary>
    public static List<(int X, int Y)> DeserializePath(string packed)
    {
        if (string.IsNullOrEmpty(packed))
            return [];

        var positions = new List<(int X, int Y)>(packed.Length);
        for (var i = 0; i < packed.Length; i++) {
            var charStr = packed[i].ToString();
            var (x, y) = UnpackPosition(charStr);
            positions.Add((x, y));
        }

        return positions;
    }

    /// <summary>
    /// Get the next movement direction from a creep's current position toward the first position in a cached path.
    /// Returns null if no valid direction exists.
    /// </summary>
    public static Direction? GetDirectionToFirstPathPosition(RoomObjectSnapshot creep, string? packedPath)
    {
        if (string.IsNullOrEmpty(packedPath))
            return null;

        var firstChar = packedPath[0].ToString();
        var (targetX, targetY) = UnpackPosition(firstChar);

        var result = GetDirectionBetween(creep.X, creep.Y, targetX, targetY);
        return result;
    }

    /// <summary>
    /// Calculate direction from (fromX, fromY) to (toX, toY).
    /// Returns null if positions are equal or direction is invalid.
    /// </summary>
    public static Direction? GetDirectionBetween(int fromX, int fromY, int toX, int toY)
    {
        var dx = toX - fromX;
        var dy = toY - fromY;

        if (dx == 0 && dy == 0)
            return null;

        // Normalize to -1, 0, or 1
        var ndx = Math.Clamp(dx, -1, 1);
        var ndy = Math.Clamp(dy, -1, 1);

        // Map to direction
        var result = (ndx, ndy) switch
        {
            (0, -1) => Direction.Top,
            (1, -1) => Direction.TopRight,
            (1, 0) => Direction.Right,
            (1, 1) => Direction.BottomRight,
            (0, 1) => Direction.Bottom,
            (-1, 1) => Direction.BottomLeft,
            (-1, 0) => Direction.Left,
            (-1, -1) => Direction.TopLeft,
            _ => (Direction?)null
        };
        return result;
    }

    /// <summary>
    /// Calculate Chebyshev distance (max of absolute differences).
    /// </summary>
    public static int GetDistance(int x1, int y1, int x2, int y2)
    {
        var dx = Math.Abs(x2 - x1);
        var dy = Math.Abs(y2 - y1);
        var result = Math.Max(dx, dy);
        return result;
    }

    /// <summary>
    /// Calculate Chebyshev distance between two room objects.
    /// </summary>
    public static int GetDistance(RoomObjectSnapshot obj1, RoomObjectSnapshot obj2)
    {
        var result = GetDistance(obj1.X, obj1.Y, obj2.X, obj2.Y);
        return result;
    }
}
