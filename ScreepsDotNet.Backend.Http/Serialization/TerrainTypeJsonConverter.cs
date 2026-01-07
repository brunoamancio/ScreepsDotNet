namespace ScreepsDotNet.Backend.Http.Serialization;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using ScreepsDotNet.Backend.Core.Constants;

internal sealed class TerrainTypeJsonConverter : JsonConverter<TerrainType>
{
    public override TerrainType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Terrain type must be encoded as a string.");

        var value = reader.GetString();
        return value?.ToLowerInvariant() switch
        {
            "wall" => TerrainType.Wall,
            "swamp" => TerrainType.Swamp,
            "plain" => TerrainType.Plain,
            _ => throw new JsonException($"Unsupported terrain type '{value}'.")
        };
    }

    public override void Write(Utf8JsonWriter writer, TerrainType value, JsonSerializerOptions options)
    {
        var encoded = value switch
        {
            TerrainType.Wall => "wall",
            TerrainType.Swamp => "swamp",
            _ => "plain"
        };

        writer.WriteStringValue(encoded);
    }
}
