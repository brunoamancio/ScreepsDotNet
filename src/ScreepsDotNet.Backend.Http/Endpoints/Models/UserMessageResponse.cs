namespace ScreepsDotNet.Backend.Http.Endpoints.Models;

using System;
using System.Text.Json.Serialization;

internal sealed record UserMessageResponse(
    [property: JsonPropertyName("_id")] string Id,
    [property: JsonPropertyName("user")] string UserId,
    [property: JsonPropertyName("respondent")] string RespondentId,
    [property: JsonPropertyName("date")] DateTime Date,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("unread")] bool Unread,
    [property: JsonPropertyName("outMessage")] string? OutMessageId);
