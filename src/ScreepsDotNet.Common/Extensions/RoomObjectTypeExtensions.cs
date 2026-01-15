namespace ScreepsDotNet.Common.Extensions;

using System.Collections.Generic;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;

public static class RoomObjectTypeExtensions
{
    private static readonly IReadOnlyDictionary<RoomObjectType, string> DocumentValues = new Dictionary<RoomObjectType, string>(EnumComparer.Instance)
    {
        [RoomObjectType.Controller] = RoomObjectTypes.Controller,
        [RoomObjectType.Spawn] = RoomObjectTypes.Spawn,
        [RoomObjectType.Mineral] = RoomObjectTypes.Mineral,
        [RoomObjectType.InvaderCore] = RoomObjectTypes.InvaderCore,
        [RoomObjectType.PowerCreep] = RoomObjectTypes.PowerCreep
    };

    public static string ToDocumentValue(this RoomObjectType type)
        => DocumentValues[type];

    private sealed class EnumComparer : IEqualityComparer<RoomObjectType>
    {
        public static EnumComparer Instance { get; } = new();

        public bool Equals(RoomObjectType x, RoomObjectType y) => x == y;

        public int GetHashCode(RoomObjectType obj) => (int)obj;
    }
}
