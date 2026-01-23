namespace ScreepsDotNet.Common.Extensions;

using ScreepsDotNet.Common.Types;

public static class DirectionExtensions
{
    /// <summary>
    /// Direction offset mappings for all 8 directions.
    /// </summary>
    private static readonly IReadOnlyDictionary<Direction, (int dx, int dy)> DirectionOffsets = new Dictionary<Direction, (int dx, int dy)>
    {
        [Direction.Top] = (0, -1),
        [Direction.TopRight] = (1, -1),
        [Direction.Right] = (1, 0),
        [Direction.BottomRight] = (1, 1),
        [Direction.Bottom] = (0, 1),
        [Direction.BottomLeft] = (-1, 1),
        [Direction.Left] = (-1, 0),
        [Direction.TopLeft] = (-1, -1)
    };

    /// <summary>
    /// Reverse mapping from offset to direction.
    /// </summary>
    private static readonly IReadOnlyDictionary<(int dx, int dy), Direction> OffsetDirections = new Dictionary<(int dx, int dy), Direction>
    {
        [(0, -1)] = Direction.Top,
        [(1, -1)] = Direction.TopRight,
        [(1, 0)] = Direction.Right,
        [(1, 1)] = Direction.BottomRight,
        [(0, 1)] = Direction.Bottom,
        [(-1, 1)] = Direction.BottomLeft,
        [(-1, 0)] = Direction.Left,
        [(-1, -1)] = Direction.TopLeft
    };

    public static bool TryParseDirection(int value, out Direction direction)
    {
        if (value is >= 1 and <= 8) {
            direction = (Direction)value;
            return true;
        }

        direction = default;
        return false;
    }

    public static int ToInt(this Direction direction)
        => (int)direction;

    /// <summary>
    /// Converts a Direction to X/Y offset coordinates.
    /// </summary>
    public static (int dx, int dy) ToOffset(this Direction direction)
    {
        var result = DirectionOffsets.TryGetValue(direction, out var offset) ? offset : (0, 0);
        return result;
    }

    /// <summary>
    /// Converts an integer direction value (1-8) to X/Y offset coordinates.
    /// </summary>
    public static (int dx, int dy) ToOffset(int directionValue)
    {
        if (!TryParseDirection(directionValue, out var direction))
            return (0, 0);

        var result = direction.ToOffset();
        return result;
    }

    /// <summary>
    /// Converts X/Y offset coordinates to a Direction.
    /// Returns null if the offset doesn't map to a valid direction.
    /// </summary>
    public static Direction? ToDirection(int dx, int dy)
    {
        var result = OffsetDirections.TryGetValue((dx, dy), out var direction) ? (Direction?)direction : null;
        return result;
    }

    /// <summary>
    /// Gets all direction offsets (useful for iteration).
    /// </summary>
    public static IReadOnlyDictionary<Direction, (int dx, int dy)> GetAllOffsets()
        => DirectionOffsets;
}
