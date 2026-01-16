namespace ScreepsDotNet.Engine.Processors.Helpers;

using System;
using System.Collections.Generic;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.Models;

internal interface ISpawnStateReader
{
    SpawnRuntimeState GetState(RoomState state, RoomObjectSnapshot spawn);
}

internal sealed class SpawnStateReader : ISpawnStateReader
{
    public SpawnRuntimeState GetState(RoomState state, RoomObjectSnapshot spawn)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(spawn);

        var inProgress = spawn.Spawning;
        var pendingCreep = inProgress is null ? null : FindPendingCreep(state.Objects, spawn, inProgress.Name);
        var remainingTicks = CalculateRemainingTicks(inProgress, state.GameTime);

        return new SpawnRuntimeState(spawn, inProgress, pendingCreep, remainingTicks);
    }

    private static RoomObjectSnapshot? FindPendingCreep(
        IReadOnlyDictionary<string, RoomObjectSnapshot> objects,
        RoomObjectSnapshot spawn,
        string? creepName)
    {
        if (string.IsNullOrWhiteSpace(creepName))
            return null;

        foreach (var obj in objects.Values)
        {
            if (obj.Type != RoomObjectTypes.Creep)
                continue;

            if (!string.Equals(obj.Name, creepName, StringComparison.Ordinal))
                continue;

            if (!string.Equals(obj.UserId, spawn.UserId, StringComparison.Ordinal))
                continue;

            if (obj.X != spawn.X || obj.Y != spawn.Y)
                continue;

            return obj;
        }

        return null;
    }

    private static int? CalculateRemainingTicks(RoomSpawnSpawningSnapshot? spawning, int gameTime)
        => spawning?.SpawnTime is { } spawnTime
            ? Math.Max(spawnTime - gameTime, 0)
            : null;
}

internal sealed record SpawnRuntimeState(RoomObjectSnapshot Spawn, RoomSpawnSpawningSnapshot? Spawning, RoomObjectSnapshot? PendingCreep, int? RemainingTime)
{
    public bool IsSpawning => Spawning is not null;
    public IReadOnlyList<Direction>? Directions => Spawning?.Directions;
    public string? PendingCreepName => Spawning?.Name ?? PendingCreep?.Name;
}
