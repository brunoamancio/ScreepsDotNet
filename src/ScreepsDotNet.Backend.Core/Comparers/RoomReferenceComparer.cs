namespace ScreepsDotNet.Backend.Core.Comparers;

using ScreepsDotNet.Backend.Core.Models;

public sealed class RoomReferenceComparer : IEqualityComparer<RoomReference>
{
    public static RoomReferenceComparer OrdinalIgnoreCase { get; } = new();

    public bool Equals(RoomReference? x, RoomReference? y)
    {
        if (ReferenceEquals(x, y))
            return true;
        if (x is null || y is null)
            return false;

        return string.Equals(x.RoomName, y.RoomName, StringComparison.OrdinalIgnoreCase)
               && string.Equals(x.ShardName ?? string.Empty, y.ShardName ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    public int GetHashCode(RoomReference obj)
    {
        var roomHash = StringComparer.OrdinalIgnoreCase.GetHashCode(obj.RoomName);
        var shardHash = StringComparer.OrdinalIgnoreCase.GetHashCode(obj.ShardName ?? string.Empty);
        return HashCode.Combine(roomHash, shardHash);
    }
}
