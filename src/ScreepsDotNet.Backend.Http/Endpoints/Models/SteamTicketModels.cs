namespace ScreepsDotNet.Backend.Http.Endpoints.Models;

using System.Text.Json.Serialization;

internal sealed record SteamTicketRequest(
    [property: JsonPropertyName("ticket")] string Ticket,
    [property: JsonPropertyName("useNativeAuth")] bool UseNativeAuth);

internal sealed record SteamTicketResponse(
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("steamid")] string SteamId);

internal sealed record ErrorResponse(
    [property: JsonPropertyName("error")] string Error);
