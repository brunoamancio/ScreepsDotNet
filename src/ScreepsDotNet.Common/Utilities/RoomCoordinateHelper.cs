namespace ScreepsDotNet.Common.Utilities;

using System;

/// <summary>
/// Helpers for converting Screeps room names to Cartesian coordinates and computing distances.
/// </summary>
public static class RoomCoordinateHelper
{
    /// <summary>
    /// Converts a Screeps room name (e.g. W10N5) into world coordinates where the origin is at E0S0.
    /// </summary>
    public static (int X, int Y) ToCoordinates(string roomName)
    {
        if (string.IsNullOrWhiteSpace(roomName))
            throw new ArgumentException("Room name cannot be null or empty.", nameof(roomName));

        roomName = roomName.Trim();
        if (roomName.Length < 3)
            throw new FormatException($"Room name '{roomName}' is too short.");

        var horizontalDir = roomName[0];
        var index = 1;
        while (index < roomName.Length && char.IsDigit(roomName[index]))
            index++;

        if (index == roomName.Length)
            throw new FormatException($"Room name '{roomName}' is missing the vertical half.");

        if (!int.TryParse(roomName.AsSpan(1, index - 1), out var horizontalMagnitude))
            throw new FormatException($"Room name '{roomName}' has an invalid horizontal magnitude.");

        var verticalDir = roomName[index];
        var verticalSpan = roomName[(index + 1)..];
        if (verticalSpan.Length == 0)
            throw new FormatException($"Room name '{roomName}' is missing the vertical magnitude.");

        if (!int.TryParse(verticalSpan, out var verticalMagnitude))
            throw new FormatException($"Room name '{roomName}' has an invalid vertical magnitude.");

        var x = horizontalDir is 'W' or 'w'
            ? -horizontalMagnitude - 1
            : horizontalMagnitude;
        var y = verticalDir is 'N' or 'n'
            ? -verticalMagnitude - 1
            : verticalMagnitude;

        return (x, y);
    }

    /// <summary>
    /// Attempts to parse a room name into coordinates without throwing on failure.
    /// </summary>
    public static bool TryParse(string roomName, out int x, out int y)
    {
        try {
            (x, y) = ToCoordinates(roomName);
            return true;
        }
        catch {
            x = 0;
            y = 0;
            return false;
        }
    }

    /// <summary>
    /// Converts world coordinates back into a Screeps room name (e.g. (-1, -1) =&gt; W0N0).
    /// </summary>
    public static string FromCoordinates(int x, int y)
    {
        static string Encode(char positivePrefix, char negativePrefix, int value)
        {
            if (value < 0)
                return $"{negativePrefix}{-value - 1}";
            return $"{positivePrefix}{value}";
        }

        var horizontal = Encode('E', 'W', x);
        var vertical = Encode('S', 'N', y);
        return $"{horizontal}{vertical}";
    }

    /// <summary>
    /// Alias for <see cref="FromCoordinates"/> to mirror the legacy helper name.
    /// </summary>
    public static string ToRoomName(int x, int y) => FromCoordinates(x, y);

    /// <summary>
    /// Calculates Chebyshev distance between two rooms. When <paramref name="wrapWorld"/> is true and
    /// <paramref name="worldSize"/> is provided, distances wrap around the edges (as on persistent shards).
    /// </summary>
    public static int CalculateDistance(string roomA, string roomB, bool wrapWorld = false, int? worldSize = null)
    {
        var (ax, ay) = ToCoordinates(roomA);
        var (bx, by) = ToCoordinates(roomB);

        var dx = Math.Abs(ax - bx);
        var dy = Math.Abs(ay - by);

        if (wrapWorld && worldSize is { } size && size > 0) {
            dx %= size;
            dy %= size;
            dx = Math.Min(dx, size - dx);
            dy = Math.Min(dy, size - dy);
        }

        return Math.Max(dx, dy);
    }
}
