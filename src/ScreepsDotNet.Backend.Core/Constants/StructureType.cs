namespace ScreepsDotNet.Backend.Core.Constants;

using System.Text.Json;
using System.Text.Json.Serialization;
using ScreepsDotNet.Common.Constants;

[JsonConverter(typeof(StructureTypeJsonConverter))]
public enum StructureType
{
    Spawn,
    Extension,
    Road,
    Wall,
    Rampart,
    Link,
    Storage,
    Tower,
    Observer,
    PowerSpawn,
    Extractor,
    Lab,
    Terminal,
    Container,
    Nuker,
    Factory,
    ConstructedWall,
    Controller,
    InvaderCore,
    Source,
    Mineral,
    ConstructionSite,
    Creep,
    Ruin,
    Exit
}

public sealed class StructureTypeJsonConverter : JsonConverter<StructureType>
{
    public override StructureType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (value != null && value.TryParseStructureType(out var structureType)) return structureType;

        throw new JsonException($"Unknown structure type: {value}");
    }

    public override void Write(Utf8JsonWriter writer, StructureType value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToDocumentValue());
}

public static class StructureTypeExtensions
{
    private static readonly Dictionary<StructureType, string> ToDocumentValueMap = new()
    {
        [StructureType.Spawn] = RoomObjectTypes.Spawn,
        [StructureType.Extension] = RoomObjectTypes.Extension,
        [StructureType.Road] = RoomObjectTypes.Road,
        [StructureType.Wall] = RoomObjectTypes.Wall,
        [StructureType.Rampart] = RoomObjectTypes.Rampart,
        [StructureType.Link] = RoomObjectTypes.Link,
        [StructureType.Storage] = RoomObjectTypes.Storage,
        [StructureType.Tower] = RoomObjectTypes.Tower,
        [StructureType.Observer] = RoomObjectTypes.Observer,
        [StructureType.PowerSpawn] = RoomObjectTypes.PowerSpawn,
        [StructureType.Extractor] = RoomObjectTypes.Extractor,
        [StructureType.Lab] = RoomObjectTypes.Lab,
        [StructureType.Terminal] = RoomObjectTypes.Terminal,
        [StructureType.Container] = RoomObjectTypes.Container,
        [StructureType.Nuker] = RoomObjectTypes.Nuker,
        [StructureType.Factory] = RoomObjectTypes.Factory,
        [StructureType.ConstructedWall] = RoomObjectTypes.ConstructedWall,
        [StructureType.Controller] = RoomObjectTypes.Controller,
        [StructureType.InvaderCore] = RoomObjectTypes.InvaderCore,
        [StructureType.Source] = RoomObjectTypes.Source,
        [StructureType.Mineral] = RoomObjectTypes.Mineral,
        [StructureType.ConstructionSite] = RoomObjectTypes.ConstructionSite,
        [StructureType.Creep] = RoomObjectTypes.Creep,
        [StructureType.Ruin] = RoomObjectTypes.Ruin,
        [StructureType.Exit] = RoomObjectTypes.Exit
    };

    private static readonly Dictionary<string, StructureType> FromDocumentValueMap = ToDocumentValueMap.ToDictionary(kvp => kvp.Value, kvp => kvp.Key, StringComparer.OrdinalIgnoreCase);

    public static string ToDocumentValue(this StructureType structureType)
        => ToDocumentValueMap.TryGetValue(structureType, out var value) ? value : throw new ArgumentOutOfRangeException(nameof(structureType), structureType, null);

    public static StructureType ToStructureType(this string value)
        => FromDocumentValueMap.TryGetValue(value, out var structureType) ? structureType : throw new ArgumentException($"Unknown structure type: {value}", nameof(value));

    public static bool TryParseStructureType(this string value, out StructureType structureType)
        => FromDocumentValueMap.TryGetValue(value, out structureType);
}
