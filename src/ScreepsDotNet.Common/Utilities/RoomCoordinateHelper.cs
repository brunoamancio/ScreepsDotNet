namespace ScreepsDotNet.Common.Utilities;

using System;

/// <summary>
/// Utility helpers for converting Screeps room names (e.g. "W8N3") to X/Y coordinates and back.
/// Mirrors the legacy engine logic so driver and engine share the same mapping.
/// </summary>
public static class RoomCoordinateHelper
{
    public static bool TryParse(string roomName, out int x, out int y)
    {
        x = 0;
        y = 0;

        if (string.IsNullOrWhiteSpace(roomName) || roomName.Length < 3)
            return false;

        // Determine split between horizontal and vertical sections (handles 1-3 digit coordinates).
        var verticalIndex = FindVerticalIndex(roomName);
        if (verticalIndex <= 0 || verticalIndex >= roomName.Length - 1)
            return false;

        if (!int.TryParse(roomName.AsSpan(1, verticalIndex - 1), out var horizontal))
            return false;
        if (!int.TryParse(roomName.AsSpan(verticalIndex + 1), out var vertical))
            return false;

        var horizontalDir = roomName[0];
        var verticalDir = roomName[verticalIndex];

        x = horizontalDir is 'W' or 'w'
            ? -horizontal - 1
            : horizontal;

        y = verticalDir is 'N' or 'n'
            ? -vertical - 1
            : vertical;

        return true;
    }

    public static string ToRoomName(int x, int y)
    {
        var horizontal = x < 0 ? $"W{-x - 1}" : $"E{x}";
        var vertical = y < 0 ? $"N{-y - 1}" : $"S{y}";
        return string.Create(horizontal.Length + vertical.Length, (horizontal, vertical), static (span, state) =>
        {
            state.horizontal.AsSpan().CopyTo(span);
            state.vertical.AsSpan().CopyTo(span[state.horizontal.Length..]);
        });
    }

    private static int FindVerticalIndex(string roomName)
    {
        for (var i = 1; i < roomName.Length; i++)
        {
            var c = roomName[i];
            if (c is 'N' or 'n' or 'S' or 's')
                return i;
        }

        return -1;
    }
}
