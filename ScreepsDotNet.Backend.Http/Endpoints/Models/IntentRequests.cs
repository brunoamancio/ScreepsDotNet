namespace ScreepsDotNet.Backend.Http.Endpoints.Models;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed record AddObjectIntentRequest
{
    [JsonPropertyName("room")]
    public string Room { get; init; } = string.Empty;

    [JsonPropertyName("_id")]
    public string ObjectId { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string IntentName { get; init; } = string.Empty;

    [JsonPropertyName("intent")]
    public JsonElement IntentPayload { get; init; }

    public bool IsValid()
        => !string.IsNullOrWhiteSpace(Room) && !string.IsNullOrWhiteSpace(ObjectId) && !string.IsNullOrWhiteSpace(IntentName)
                                            && IntentPayload.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null);
}

public sealed record AddGlobalIntentRequest
{
    [JsonPropertyName("name")]
    public string IntentName { get; init; } = string.Empty;

    [JsonPropertyName("intent")]
    public JsonElement IntentPayload { get; init; }

    public bool HasPayload
        => !string.IsNullOrWhiteSpace(IntentName) && IntentPayload.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null);
}
