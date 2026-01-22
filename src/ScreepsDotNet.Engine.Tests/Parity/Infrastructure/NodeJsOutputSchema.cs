namespace ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

using System.Text.Json.Serialization;

/// <summary>
/// Schema for Node.js harness output (from output-serializer.js)
/// </summary>
public sealed record NodeJsOutput(
    [property: JsonPropertyName("mutations")] NodeJsMutations Mutations,
    [property: JsonPropertyName("stats")] Dictionary<string, object>? Stats,
    [property: JsonPropertyName("actionLogs")] Dictionary<string, object>? ActionLogs,
    [property: JsonPropertyName("finalState")] Dictionary<string, object>? FinalState,
    [property: JsonPropertyName("metadata")] NodeJsMetadata Metadata);

public sealed record NodeJsMutations(
    [property: JsonPropertyName("patches")] List<NodeJsPatch> Patches,
    [property: JsonPropertyName("upserts")] List<Dictionary<string, object>>? Upserts,
    [property: JsonPropertyName("removals")] List<string>? Removals);

public sealed record NodeJsPatch(
    [property: JsonPropertyName("objectId")] string ObjectId)
{
    // Additional properties are dynamic (store, energy, hits, etc.)
    // Use JsonExtensionData to capture them
    [JsonExtensionData]
    public Dictionary<string, object>? AdditionalData { get; init; }
}

public sealed record NodeJsMetadata(
    [property: JsonPropertyName("room")] string Room,
    [property: JsonPropertyName("gameTime")] int GameTime,
    [property: JsonPropertyName("timestamp")] string Timestamp);
