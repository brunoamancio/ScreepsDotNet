namespace ScreepsDotNet.Backend.Http.Endpoints.Models;

using System.Text.Json.Serialization;

internal sealed record SetUsernameRequest(
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("email")] string? Email);
