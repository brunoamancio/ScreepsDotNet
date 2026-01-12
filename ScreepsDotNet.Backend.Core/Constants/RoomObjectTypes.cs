namespace ScreepsDotNet.Backend.Core.Constants;

using System.Collections.Generic;
using ScreepsDotNet.Common;

public enum RoomObjectType
{
    Controller,
    Spawn,
    Mineral,
    InvaderCore,
    PowerCreep
}

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
