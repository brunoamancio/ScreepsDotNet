namespace ScreepsDotNet.Backend.Http.Endpoints.Models;

using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed record UserBadgeUpdateRequest([property: JsonPropertyName("badge")] UserBadgePayload? Badge);

internal sealed record UserBadgePayload([property: JsonPropertyName("type")] JsonElement Type,
                                        [property: JsonPropertyName("param")] double? Param,
                                        [property: JsonPropertyName("color1")] string? Color1,
                                        [property: JsonPropertyName("color2")] string? Color2,
                                        [property: JsonPropertyName("color3")] string? Color3,
                                        [property: JsonPropertyName("flip")] bool? Flip);

internal sealed record EmailUpdateRequest([property: JsonPropertyName("email")] string? Email);

internal sealed record SetSteamVisibleRequest([property: JsonPropertyName("visible")] bool? Visible);
