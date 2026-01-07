namespace ScreepsDotNet.Backend.Http.Endpoints.Helpers;

using System;
using System.Collections.Generic;
using System.Linq;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Http.Endpoints.Models;

internal static class WorldResponseFactory
{
    public static MapStatsResponseModel CreateMapStatsResponse(MapStatsResult result)
    {
        var stats = result.Stats.Values
                              .Select(room => new KeyValuePair<string, MapStatsRoomResponse>(room.RoomName,
                                  new MapStatsRoomResponse(room.Status,
                                                           room.IsNoviceArea,
                                                           room.IsRespawnArea,
                                                           room.OpenTime,
                                                           room.Ownership is null ? null : new MapStatsOwnershipResponse(room.Ownership.UserId, room.Ownership.Level),
                                                           room.Sign is null ? null : new MapStatsSignResponse(room.Sign.UserId, room.Sign.Text, room.Sign.Time),
                                                           room.IsSafeMode,
                                                           room.PrimaryMineral is null ? null : new MapStatsMineralResponse(room.PrimaryMineral.Type, room.PrimaryMineral.Density))))
                              .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        var users = result.Users.Values.Select(user => new KeyValuePair<string, MapStatsUserResponse>(user.Id, new MapStatsUserResponse(user.Id, user.Username, user.Badge)))
                                       .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        return new MapStatsResponseModel(result.GameTime, stats, result.StatsMax, users);
    }

    public static RoomStatusResponse CreateRoomStatusResponse(RoomStatusInfo? status)
    {
        if (status is null)
            return new RoomStatusResponse(null);

        var details = new RoomStatusDetailsResponse(status.Status, status.IsNoviceArea, status.IsRespawnArea, status.OpenTime);
        return new RoomStatusResponse(details);
    }

    public static RoomTerrainResponse CreateEncodedTerrainResponse(IReadOnlyList<RoomTerrainData> entries)
    {
        var payload = entries.Select(object (entry) => new RoomTerrainEncodedEntryResponse(entry.RoomName, entry.Type, entry.Terrain))
                             .ToList();
        return new RoomTerrainResponse(payload);
    }

    public static RoomTerrainResponse CreateDecodedTerrainResponse(IReadOnlyList<RoomTerrainData> entries)
    {
        var payload = entries.Select(entry =>
        {
            if (!string.Equals(entry.Type, "terrain", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(entry.Terrain))
                return new RoomTerrainDecodedEntryResponse(entry.RoomName, entry.Type, Array.Empty<TerrainTileResponse>());

            var tiles = TerrainEncodingHelper.Decode(entry.Terrain!);
            return new RoomTerrainDecodedEntryResponse(entry.RoomName, entry.Type, tiles);
        }).ToList();

        return new RoomTerrainResponse(payload);
    }

    public static RoomsResponse CreateRoomsResponse(IReadOnlyList<RoomTerrainData> entries)
    {
        var rooms = entries.Select(entry => new RoomTerrainEncodedEntryResponse(entry.RoomName, entry.Type, entry.Terrain))
                           .ToList();
        return new RoomsResponse(rooms);
    }

    public static WorldSizeResponse CreateWorldSizeResponse(WorldSize worldSize)
        => new(worldSize.Width, worldSize.Height);

    public static WorldTimeResponse CreateTimeResponse(int time)
        => new(time);

    public static WorldTickResponse CreateTickResponse(int tick)
        => new(tick);

}
