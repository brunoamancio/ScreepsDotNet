namespace ScreepsDotNet.Backend.Http.Endpoints.Helpers;

using System.Collections.Generic;
using System.Linq;
using ScreepsDotNet.Backend.Core.Comparers;
using ScreepsDotNet.Backend.Core.Models;

internal static class RoomReferenceParser
{
    public static bool TryParse(string? value, string? overrideShard, out RoomReference? reference)
    {
        reference = null;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        var room = trimmed;
        string? shardFromValue = null;

        var slashIndex = trimmed.IndexOf('/');
        if (slashIndex >= 0) {
            if (slashIndex == 0 || slashIndex == trimmed.Length - 1)
                return false;

            shardFromValue = trimmed[..slashIndex];
            room = trimmed[(slashIndex + 1)..];

            if (string.IsNullOrWhiteSpace(shardFromValue) || string.IsNullOrWhiteSpace(room))
                return false;
        }

        var resolvedShard = string.IsNullOrWhiteSpace(overrideShard)
            ? shardFromValue
            : overrideShard!.Trim();

        reference = RoomReference.Create(room, resolvedShard);
        return true;
    }

    public static bool TryParseRooms(IEnumerable<string>? values, string? overrideShard, out IReadOnlyList<RoomReference> rooms)
    {
        rooms = [];
        if (values is null)
            return false;

        var parsed = new List<RoomReference>();
        foreach (var value in values) {
            if (!TryParse(value, overrideShard, out var reference) || reference is null)
                return false;

            parsed.Add(reference);
        }

        if (parsed.Count == 0)
            return false;

        rooms = parsed.Distinct(RoomReferenceComparer.OrdinalIgnoreCase).ToList();
        return rooms.Count > 0;
    }
}
