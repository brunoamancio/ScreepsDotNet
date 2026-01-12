namespace ScreepsDotNet.Backend.Http.Endpoints.Models;

using System.Text.Json.Serialization;

internal sealed record SendUserMessageRequest(
    [property: JsonPropertyName("respondent")] string? Respondent,
    [property: JsonPropertyName("text")] string? Text);
