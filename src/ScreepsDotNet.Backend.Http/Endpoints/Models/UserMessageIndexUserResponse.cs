namespace ScreepsDotNet.Backend.Http.Endpoints.Models;

using System.Collections.Generic;
using System.Text.Json.Serialization;

internal sealed record UserMessageIndexUserResponse(
    [property: JsonPropertyName("_id")] string Id,
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("badge")] IReadOnlyDictionary<string, object?>? Badge);
