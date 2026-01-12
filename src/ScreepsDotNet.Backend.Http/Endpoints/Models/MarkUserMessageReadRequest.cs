namespace ScreepsDotNet.Backend.Http.Endpoints.Models;

using System.Text.Json.Serialization;

internal sealed record MarkUserMessageReadRequest([property: JsonPropertyName("id")] string? MessageId);
