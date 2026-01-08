namespace ScreepsDotNet.Backend.Core.Constants;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

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
        [StructureType.Spawn] = "spawn",
        [StructureType.Extension] = "extension",
        [StructureType.Road] = "road",
        [StructureType.Wall] = "wall",
        [StructureType.Rampart] = "rampart",
        [StructureType.Link] = "link",
        [StructureType.Storage] = "storage",
        [StructureType.Tower] = "tower",
        [StructureType.Observer] = "observer",
        [StructureType.PowerSpawn] = "powerSpawn",
        [StructureType.Extractor] = "extractor",
        [StructureType.Lab] = "lab",
        [StructureType.Terminal] = "terminal",
        [StructureType.Container] = "container",
        [StructureType.Nuker] = "nuker",
        [StructureType.Factory] = "factory",
        [StructureType.ConstructedWall] = "constructedWall",
        [StructureType.Controller] = "controller",
        [StructureType.InvaderCore] = "invaderCore",
        [StructureType.Source] = "source",
        [StructureType.Mineral] = "mineral",
        [StructureType.ConstructionSite] = "constructionSite",
        [StructureType.Creep] = "creep",
        [StructureType.Ruin] = "ruin",
        [StructureType.Exit] = "exit"
    };

    private static readonly Dictionary<string, StructureType> FromDocumentValueMap = ToDocumentValueMap.ToDictionary(kvp => kvp.Value, kvp => kvp.Key, StringComparer.OrdinalIgnoreCase);

    public static string ToDocumentValue(this StructureType structureType)
        => ToDocumentValueMap.TryGetValue(structureType, out var value) ? value : throw new ArgumentOutOfRangeException(nameof(structureType), structureType, null);

    public static StructureType ToStructureType(this string value)
        => FromDocumentValueMap.TryGetValue(value, out var structureType) ? structureType : throw new ArgumentException($"Unknown structure type: {value}", nameof(value));

    public static bool TryParseStructureType(this string value, out StructureType structureType)
        => FromDocumentValueMap.TryGetValue(value, out structureType);
}
