namespace ScreepsDotNet.Engine.Processors.Steps;

using System;
using System.Collections.Generic;
using System.Linq;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.Helpers;
using ScreepsDotNet.Common.Utilities;

/// <summary>
/// Handles spawn completion (creep birth) and spawn-stomp logic once the spawn timer elapses.
/// </summary>
internal sealed class SpawnSpawningStep(ISpawnStateReader spawnStateReader, ICreepDeathProcessor deathProcessor) : IRoomProcessorStep
{
    private static readonly StringComparer Comparer = StringComparer.Ordinal;

    private static readonly Direction[] AllDirections =
    [
        Direction.Top,
        Direction.TopRight,
        Direction.Right,
        Direction.BottomRight,
        Direction.Bottom,
        Direction.BottomLeft,
        Direction.Left,
        Direction.TopLeft
    ];

    internal static readonly IReadOnlyDictionary<Direction, (int X, int Y)> DirectionOffsets = new Dictionary<Direction, (int X, int Y)>
    {
        [Direction.Top] = (0, -1),
        [Direction.TopRight] = (1, -1),
        [Direction.Right] = (1, 0),
        [Direction.BottomRight] = (1, 1),
        [Direction.Bottom] = (0, 1),
        [Direction.BottomLeft] = (-1, 1),
        [Direction.Left] = (-1, 0),
        [Direction.TopLeft] = (-1, -1)
    };

    private static readonly HashSet<string> BlockingStructures = new(StringComparer.Ordinal)
    {
        RoomObjectTypes.Spawn,
        RoomObjectTypes.Extension,
        RoomObjectTypes.Tower,
        RoomObjectTypes.Lab,
        RoomObjectTypes.Nuker,
        RoomObjectTypes.Observer,
        RoomObjectTypes.PowerSpawn,
        RoomObjectTypes.Factory,
        RoomObjectTypes.Terminal,
        RoomObjectTypes.Storage,
        RoomObjectTypes.Link,
        RoomObjectTypes.ConstructedWall
    };

    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        var terrain = BuildTerrainCache(context);
        var tiles = BuildTileMap(context);
        var energyLedger = new Dictionary<string, int>(Comparer);

        foreach (var obj in context.State.Objects.Values)
        {
            if (!string.Equals(obj.Type, RoomObjectTypes.Spawn, StringComparison.Ordinal))
                continue;

            var runtime = spawnStateReader.GetState(context.State, obj);
            if (!runtime.IsSpawning || runtime.Spawning is null)
                continue;

            if (!IsReady(runtime.Spawning, context.State.GameTime))
                continue;

            var placeholder = runtime.PendingCreep;
            if (placeholder is null)
            {
                DelaySpawn(context, obj, runtime.Spawning);
                continue;
            }

            var placement = TryPlaceCreep(context, runtime, placeholder, tiles, terrain, energyLedger);
            if (placement is null)
            {
                DelaySpawn(context, obj, runtime.Spawning);
                continue;
            }

            context.MutationWriter.Upsert(placement.Value.Creep);
            context.MutationWriter.Patch(obj.Id, new RoomObjectPatchPayload
            {
                ClearSpawning = true
            });

            UpdateTileMapAfterPlacement(tiles, placeholder, placement.Value.Target);
        }

        return Task.CompletedTask;
    }

    private static bool IsReady(RoomSpawnSpawningSnapshot spawning, int gameTime)
        => spawning.SpawnTime is { } spawnTime && gameTime >= (spawnTime - 1);

    private static void DelaySpawn(RoomProcessorContext context, RoomObjectSnapshot spawn, RoomSpawnSpawningSnapshot spawning)
    {
        var deferred = spawning with { SpawnTime = context.State.GameTime + 1 };
        context.MutationWriter.Patch(spawn.Id, new RoomObjectPatchPayload
        {
            Spawning = deferred
        });
    }

    private PlacementResult? TryPlaceCreep(
        RoomProcessorContext context,
        SpawnRuntimeState runtime,
        RoomObjectSnapshot placeholder,
        Dictionary<TileCoord, TileInfo> tiles,
        TerrainCache terrain,
        IDictionary<string, int> energyLedger)
    {
        var spawn = runtime.Spawn;
        var preferred = runtime.Directions is { Count: > 0 } ? runtime.Directions : AllDirections;
        var hostileCandidate = default(RoomObjectSnapshot);
        var hostileCoord = default(TileCoord?);

        foreach (var direction in preferred)
        {
            if (!DirectionOffsets.TryGetValue(direction, out var offset))
                continue;

            var target = new TileCoord(spawn.X + offset.X, spawn.Y + offset.Y);
            if (!IsWithinBounds(target))
                continue;

            var occupancy = AnalyzeTile(tiles, terrain, target, spawn.UserId);
            if (!occupancy.IsBlocked)
                return CreatePlacement(placeholder, target);

            if (hostileCandidate is null && occupancy.HostileOccupant is not null)
            {
                hostileCandidate = occupancy.HostileOccupant;
                hostileCoord = target;
            }
        }

        if (hostileCandidate is not null && hostileCoord is not null && !HasAlternateOpening(spawn, preferred, tiles, terrain))
        {
            deathProcessor.Process(
                context,
                hostileCandidate,
                new CreepDeathOptions(ViolentDeath: true, Spawn: spawn),
                energyLedger);

            RemoveOccupant(tiles, hostileCoord.Value, hostileCandidate.Id);
            return CreatePlacement(placeholder, hostileCoord.Value);
        }

        return null;
    }

    private static bool HasAlternateOpening(
        RoomObjectSnapshot spawn,
        IReadOnlyCollection<Direction> preferred,
        Dictionary<TileCoord, TileInfo> tiles,
        TerrainCache terrain)
    {
        foreach (var direction in AllDirections)
        {
            if (preferred.Contains(direction))
                continue;

            if (!DirectionOffsets.TryGetValue(direction, out var offset))
                continue;

            var target = new TileCoord(spawn.X + offset.X, spawn.Y + offset.Y);
            if (!IsWithinBounds(target))
                continue;

            var occupancy = AnalyzeTile(tiles, terrain, target, spawn.UserId);
            if (!occupancy.IsBlocked)
                return true;
        }

        return false;
    }

    private static bool IsWithinBounds(TileCoord coord)
        => coord.X is >= 0 and <= 49 && coord.Y is >= 0 and <= 49;

    private static PlacementResult CreatePlacement(RoomObjectSnapshot placeholder, TileCoord target)
    {
        var updated = placeholder with
        {
            X = target.X,
            Y = target.Y,
            IsSpawning = false
        };

        return new PlacementResult(updated, target);
    }

    private static void UpdateTileMapAfterPlacement(
        Dictionary<TileCoord, TileInfo> tiles,
        RoomObjectSnapshot placeholder,
        TileCoord destination)
    {
        var origin = new TileCoord(placeholder.X, placeholder.Y);
        if (tiles.TryGetValue(origin, out var originTile))
            originTile.Creeps.RemoveAll(creep => string.Equals(creep.Id, placeholder.Id, StringComparison.Ordinal));

        var destTile = GetOrCreateTile(tiles, destination);
        destTile.Creeps.RemoveAll(creep => string.Equals(creep.Id, placeholder.Id, StringComparison.Ordinal));
        destTile.Creeps.Add(placeholder with { X = destination.X, Y = destination.Y, IsSpawning = false });
    }

    private static void RemoveOccupant(Dictionary<TileCoord, TileInfo> tiles, TileCoord coordinate, string occupantId)
    {
        if (tiles.TryGetValue(coordinate, out var tile))
        {
            tile.Creeps.RemoveAll(creep => string.Equals(creep.Id, occupantId, StringComparison.Ordinal));
        }
    }

    private static TileOccupancy AnalyzeTile(
        Dictionary<TileCoord, TileInfo> tiles,
        TerrainCache terrain,
        TileCoord target,
        string? spawnUserId)
    {
        if (!tiles.TryGetValue(target, out var tile))
        {
            var isWall = terrain.IsWall(target.X, target.Y);
            return isWall ? TileOccupancy.Blocked : TileOccupancy.Open;
        }

        if (terrain.IsWall(target.X, target.Y) && !tile.HasRoad)
            return TileOccupancy.Blocked;

        foreach (var structure in tile.Structures)
        {
            if (string.Equals(structure.Type, RoomObjectTypes.Rampart, StringComparison.Ordinal))
            {
                if (structure.IsPublic == true || string.Equals(structure.UserId, spawnUserId, StringComparison.Ordinal))
                    continue;

                return TileOccupancy.Blocked;
            }

            if (string.Equals(structure.Type, RoomObjectTypes.ConstructionSite, StringComparison.Ordinal))
            {
                if (!BlockingStructures.Contains(structure.StructureType ?? string.Empty))
                    continue;

                return TileOccupancy.Blocked;
            }

            if (BlockingStructures.Contains(structure.Type))
                return TileOccupancy.Blocked;
        }

        if (tile.Creeps.Count == 0)
            return TileOccupancy.Open;

        var hostile = tile.Creeps.FirstOrDefault(creep =>
            !string.Equals(creep.UserId, spawnUserId, StringComparison.Ordinal));

        return hostile is null
            ? TileOccupancy.Blocked
            : TileOccupancy.Hostile(hostile);
    }

    private static Dictionary<TileCoord, TileInfo> BuildTileMap(RoomProcessorContext context)
    {
        var tiles = new Dictionary<TileCoord, TileInfo>();
        foreach (var obj in context.State.Objects.Values)
        {
            var key = new TileCoord(obj.X, obj.Y);
            var tile = GetOrCreateTile(tiles, key);

            if (string.Equals(obj.Type, RoomObjectTypes.Road, StringComparison.Ordinal))
                tile.HasRoad = true;

            if (obj.IsCreep(includePowerCreep: true))
            {
                tile.Creeps.Add(obj);
            }
            else
            {
                tile.Structures.Add(obj);
            }
        }

        return tiles;
    }

    private static TileInfo GetOrCreateTile(Dictionary<TileCoord, TileInfo> tiles, TileCoord key)
    {
        if (!tiles.TryGetValue(key, out var tile))
        {
            tile = new TileInfo();
            tiles[key] = tile;
        }

        return tile;
    }

    private static TerrainCache BuildTerrainCache(RoomProcessorContext context)
    {
        var terrain = context.State.Terrain?.Values.FirstOrDefault(
            t => string.Equals(t.RoomName, context.State.RoomName, StringComparison.Ordinal));
        return new TerrainCache(terrain?.Terrain);
    }

    private readonly record struct TileCoord(int X, int Y);

    private sealed record TileInfo
    {
        public List<RoomObjectSnapshot> Structures { get; } = new();
        public List<RoomObjectSnapshot> Creeps { get; } = new();
        public bool HasRoad { get; set; }
    }

    private sealed record TerrainCache(string? Terrain)
    {
        public bool IsWall(int x, int y)
        {
            if (string.IsNullOrEmpty(Terrain))
                return false;

            var index = (y * 50) + x;
            if (index < 0 || index >= Terrain.Length)
                return false;

            var mask = TerrainEncoding.Decode(Terrain[index]);
            return (mask & ScreepsGameConstants.TerrainMaskWall) != 0;
        }
    }

    private readonly record struct TileOccupancy(bool IsBlocked, RoomObjectSnapshot? HostileOccupant)
    {
        public static TileOccupancy Open => new(false, null);
        public static TileOccupancy Blocked => new(true, null);
        public static TileOccupancy Hostile(RoomObjectSnapshot hostile) => new(true, hostile);
    }

    private readonly record struct PlacementResult(RoomObjectSnapshot Creep, TileCoord Target);
}
