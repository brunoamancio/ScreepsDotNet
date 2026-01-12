namespace ScreepsDotNet.Backend.Http.Endpoints.Models;

using System.Collections.Generic;
using System.Text.Json.Serialization;

internal sealed record UserMessageIndexResponse(
    [property: JsonPropertyName("messages")] IReadOnlyList<UserMessageIndexEntryResponse> Messages,
    [property: JsonPropertyName("users")] IReadOnlyDictionary<string, UserMessageIndexUserResponse> Users);
