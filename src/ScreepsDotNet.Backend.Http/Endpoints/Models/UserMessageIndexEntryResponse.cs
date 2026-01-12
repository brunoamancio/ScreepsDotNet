namespace ScreepsDotNet.Backend.Http.Endpoints.Models;

using System.Text.Json.Serialization;

internal sealed record UserMessageIndexEntryResponse(
    [property: JsonPropertyName("_id")] string Id,
    [property: JsonPropertyName("message")] UserMessageResponse Message);
