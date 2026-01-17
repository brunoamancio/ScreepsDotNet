namespace ScreepsDotNet.Backend.Core.Parsing;

using System.Text.RegularExpressions;
using ScreepsDotNet.Backend.Core.Comparers;
using ScreepsDotNet.Backend.Core.Models;

/// <summary>
/// Utility helpers for parsing room identifiers that may include an optional shard prefix (e.g. <c>shard3/W10N5</c>).
/// </summary>
public static partial class RoomReferenceParser
{
    private const string RoomPattern = @"^(?<horizontal>[WE])(?<x>\d+)(?<vertical>[NS])(?<y>\d+)$";
    private const string ShardPattern = @"^[A-Za-z0-9_-]+$";

    private static readonly Regex RoomRegex = RoomRegexFactory();
    private static readonly Regex ShardRegex = ShardRegexFactory();

    public static bool TryParse(string? value, string? overrideShard, out RoomReference? reference)
    {
        reference = null;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        var roomPortion = trimmed;
        string? shardPortion = null;

        var slashIndex = trimmed.IndexOf('/');
        if (slashIndex >= 0) {
            if (slashIndex == 0 || slashIndex == trimmed.Length - 1)
                return false;

            shardPortion = trimmed[..slashIndex];
            roomPortion = trimmed[(slashIndex + 1)..];

            if (string.IsNullOrWhiteSpace(shardPortion) || string.IsNullOrWhiteSpace(roomPortion))
                return false;
        }

        if (!TryNormalizeRoomName(roomPortion, out var normalizedRoom))
            return false;

        var resolvedShard = string.IsNullOrWhiteSpace(overrideShard) ? shardPortion : overrideShard;
        if (!TryNormalizeShardName(resolvedShard, out var normalizedShard))
            return false;

        reference = RoomReference.Create(normalizedRoom, normalizedShard);
        return true;
    }

    public static bool TryParseRooms(IEnumerable<string?>? values, string? overrideShard, out IReadOnlyList<RoomReference> rooms)
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

    public static string Format(RoomReference reference)
        => reference.ShardName is null
            ? reference.RoomName
            : $"{reference.ShardName}/{reference.RoomName}";

    private static bool TryNormalizeRoomName(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim().ToUpperInvariant();
        if (!RoomRegex.IsMatch(trimmed))
            return false;

        normalized = trimmed;
        return true;
    }

    private static bool TryNormalizeShardName(string? value, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(value))
            return true;

        var trimmed = value.Trim();
        if (!ShardRegex.IsMatch(trimmed))
            return false;

        normalized = trimmed;
        return true;
    }
    [GeneratedRegex(RoomPattern, RegexOptions.CultureInvariant)]
    private static partial Regex RoomRegexFactory();

    [GeneratedRegex(ShardPattern, RegexOptions.CultureInvariant)]
    private static partial Regex ShardRegexFactory();
}
