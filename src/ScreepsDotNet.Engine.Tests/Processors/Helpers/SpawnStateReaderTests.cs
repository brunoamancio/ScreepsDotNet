namespace ScreepsDotNet.Engine.Tests.Processors.Helpers;

using System;
using System.Collections.Generic;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.Models;
using ScreepsDotNet.Engine.Processors.Helpers;

public sealed class SpawnStateReaderTests
{
    private readonly ISpawnStateReader _reader = new SpawnStateReader();

    [Fact]
    public void GetState_ReturnsIdleState_WhenSpawnIsNotSpawning()
    {
        var spawn = CreateSpawn(spawnSnapshot: null);
        var state = CreateRoomState(spawn);

        var result = _reader.GetState(state, spawn);

        Assert.False(result.IsSpawning);
        Assert.Null(result.Spawning);
        Assert.Null(result.PendingCreep);
        Assert.Null(result.RemainingTime);
    }

    [Fact]
    public void GetState_FindsPendingCreepAndRemainingTime()
    {
        Direction[] directions = [Direction.Top, Direction.TopRight];
        var spawningSnapshot = new RoomSpawnSpawningSnapshot("worker1", 9, 105, directions);
        var spawn = CreateSpawn(userId: "user1", spawnSnapshot: spawningSnapshot, x: 12, y: 18);
        var pendingCreep = CreateCreep("worker1", "user1", 12, 18);
        var state = CreateRoomState(spawn, pendingCreep, gameTime: 100);

        var result = _reader.GetState(state, spawn);

        Assert.True(result.IsSpawning);
        Assert.Same(spawningSnapshot, result.Spawning);
        Assert.Equal(5, result.RemainingTime);
        Assert.Same(pendingCreep, result.PendingCreep);
        Assert.Equal(directions, result.Directions);
    }

    private static RoomState CreateRoomState(RoomObjectSnapshot spawn, RoomObjectSnapshot? creep = null, int gameTime = 0)
    {
        var objects = new Dictionary<string, RoomObjectSnapshot>(StringComparer.Ordinal)
        {
            [spawn.Id] = spawn
        };

        if (creep is not null)
            objects[creep.Id] = creep;

        return new RoomState(
            "W1N1",
            gameTime,
            null,
            objects,
            new Dictionary<string, UserState>(StringComparer.Ordinal),
            null,
            new Dictionary<string, RoomTerrainSnapshot>(StringComparer.Ordinal),
            []);
    }

    private static RoomObjectSnapshot CreateSpawn(
        string id = "spawn1",
        string? userId = "user1",
        RoomSpawnSpawningSnapshot? spawnSnapshot = null,
        int x = 10,
        int y = 20)
        => new(
            id,
            RoomObjectTypes.Spawn,
            "W1N1",
            "shard0",
            userId,
            x,
            y,
            Hits: 5000,
            HitsMax: 5000,
            Fatigue: null,
            TicksToLive: null,
            Name: "Spawn1",
            Level: 1,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.Spawn,
            Store: new Dictionary<string, int>(StringComparer.Ordinal) { [ResourceTypes.Energy] = 300 },
            StoreCapacity: 300,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<string, object?>(),
            Spawning: spawnSnapshot,
            Body: Array.Empty<CreepBodyPartSnapshot>());

    private static RoomObjectSnapshot CreateCreep(string name, string? userId, int x, int y)
        => new(
            Guid.NewGuid().ToString("N"),
            RoomObjectTypes.Creep,
            "W1N1",
            "shard0",
            userId,
            x,
            y,
            Hits: 300,
            HitsMax: 300,
            Fatigue: 0,
            TicksToLive: 100,
            Name: name,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: null,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<string, object?>(),
            Spawning: null,
            Body: Array.Empty<CreepBodyPartSnapshot>());
}
