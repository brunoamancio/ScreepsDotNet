namespace ScreepsDotNet.Backend.Http.Endpoints.Models;

using System.Collections.Generic;
using System.Text.Json.Serialization;
using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Backend.Http.Serialization;

internal sealed record MapStatsRequestModel(
    [property: JsonPropertyName("rooms")] IReadOnlyList<string>? Rooms,
    [property: JsonPropertyName("statName")] string? StatName);

internal sealed record MapStatsResponseModel(
    [property: JsonPropertyName("gameTime")] int GameTime,
    [property: JsonPropertyName("stats")] IReadOnlyDictionary<string, MapStatsRoomResponse> Stats,
    [property: JsonPropertyName("statsMax")] IReadOnlyDictionary<string, object?> StatsMax,
    [property: JsonPropertyName("users")] IReadOnlyDictionary<string, MapStatsUserResponse> Users);

internal sealed record MapStatsRoomResponse(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("novice")] bool? IsNovice,
    [property: JsonPropertyName("respawnArea")] bool? IsRespawnArea,
    [property: JsonPropertyName("openTime")] long? OpenTime,
    [property: JsonPropertyName("own")] MapStatsOwnershipResponse? Ownership,
    [property: JsonPropertyName("sign")] MapStatsSignResponse? Sign,
    [property: JsonPropertyName("safeMode")] bool IsSafeMode,
    [property: JsonPropertyName("minerals0")] MapStatsMineralResponse? PrimaryMineral);

internal sealed record MapStatsOwnershipResponse(
    [property: JsonPropertyName("user")] string UserId,
    [property: JsonPropertyName("level")] int Level);

internal sealed record MapStatsSignResponse(
    [property: JsonPropertyName("user")] string UserId,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("time")] int? Time);

internal sealed record MapStatsMineralResponse(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("density")] int? Density);

internal sealed record MapStatsUserResponse(
    [property: JsonPropertyName("_id")] string Id,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("badge")] IReadOnlyDictionary<string, object?>? Badge);

internal sealed record RoomStatusResponse(
    [property: JsonPropertyName("room")] RoomStatusDetailsResponse? Room);

internal sealed record RoomStatusDetailsResponse(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("novice")] bool? IsNovice,
    [property: JsonPropertyName("respawnArea")] bool? IsRespawnArea,
    [property: JsonPropertyName("openTime")] long? OpenTime);

internal sealed record RoomTerrainResponse(
    [property: JsonPropertyName("terrain")] IReadOnlyList<object> Terrain);

internal sealed record RoomTerrainEncodedEntryResponse(
    [property: JsonPropertyName("room")] string Room,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("terrain")] string? Terrain);

internal sealed record RoomTerrainDecodedEntryResponse(
    [property: JsonPropertyName("room")] string Room,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("terrain")] IReadOnlyList<TerrainTileResponse> Tiles);

internal sealed record TerrainTileResponse(
    [property: JsonPropertyName("x")] int X,
    [property: JsonPropertyName("y")] int Y,
    [property: JsonPropertyName("terrain")]
    [property: JsonConverter(typeof(TerrainTypeJsonConverter))] TerrainType Terrain);

internal sealed record RoomsRequest(
    [property: JsonPropertyName("rooms")] IReadOnlyList<string>? Rooms);

internal sealed record RoomsResponse(
    [property: JsonPropertyName("rooms")] IReadOnlyList<RoomTerrainEncodedEntryResponse> Rooms);

internal sealed record WorldSizeResponse(
    [property: JsonPropertyName("width")] int Width,
    [property: JsonPropertyName("height")] int Height);

internal sealed record WorldTimeResponse(
    [property: JsonPropertyName("time")] int Time);

internal sealed record WorldTickResponse(
    [property: JsonPropertyName("tick")] int Tick);
