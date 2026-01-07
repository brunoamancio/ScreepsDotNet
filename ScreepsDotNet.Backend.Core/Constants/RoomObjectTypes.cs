namespace ScreepsDotNet.Backend.Core.Constants;

using System.Collections.Generic;

public enum RoomObjectType
{
    Controller,
    Spawn,
    Mineral,
    InvaderCore
}

public static class RoomObjectTypeExtensions
{
    private static readonly IReadOnlyDictionary<RoomObjectType, string> DocumentValues = new Dictionary<RoomObjectType, string>(EnumComparer.Instance)
    {
        [RoomObjectType.Controller] = "controller",
        [RoomObjectType.Spawn] = "spawn",
        [RoomObjectType.Mineral] = "mineral",
        [RoomObjectType.InvaderCore] = "invaderCore"
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
