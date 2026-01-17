namespace ScreepsDotNet.Backend.Http.Endpoints.Helpers;

using System;
using System.Collections.Generic;
using ScreepsDotNet.Backend.Core.Extensions;
using ScreepsDotNet.Backend.Http.Endpoints.Models;
using ScreepsDotNet.Common.Utilities;

internal static class TerrainEncodingHelper
{
    private const int RoomSize = 50;
    private const int MaxTileCount = RoomSize * RoomSize;

    public static IReadOnlyList<TerrainTileResponse> Decode(string terrain)
    {
        var tileCount = Math.Min(terrain.Length, MaxTileCount);
        var tiles = new List<TerrainTileResponse>(tileCount);
        for (var index = 0; index < tileCount; index++) {
            var value = TerrainEncoding.Decode(terrain[index]);
            var x = index % RoomSize;
            var y = index / RoomSize;
            tiles.Add(new TerrainTileResponse(x, y, value.ToTerrainType()));
        }

        return tiles;
    }
}
