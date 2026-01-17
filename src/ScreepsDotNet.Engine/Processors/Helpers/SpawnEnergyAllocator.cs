namespace ScreepsDotNet.Engine.Processors.Helpers;

using System;
using System.Collections.Generic;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Contracts;

internal interface ISpawnEnergyAllocator
{
    EnergyAllocationResult AllocateEnergy(IReadOnlyDictionary<string, RoomObjectSnapshot> roomObjects, RoomObjectSnapshot spawn,
                                          int requiredEnergy, IReadOnlyList<string>? preferredStructureIds,
                                          IReadOnlyDictionary<string, int>? energyOverrides = null);
}

internal sealed class SpawnEnergyAllocator : ISpawnEnergyAllocator
{
    public EnergyAllocationResult AllocateEnergy(IReadOnlyDictionary<string, RoomObjectSnapshot> roomObjects, RoomObjectSnapshot spawn,
                                                 int requiredEnergy, IReadOnlyList<string>? preferredStructureIds,
                                                 IReadOnlyDictionary<string, int>? energyOverrides = null)
    {
        if (requiredEnergy <= 0)
            return EnergyAllocationResult.CreateSuccess([]);

        var candidates = EnumerateCandidates(roomObjects, spawn, preferredStructureIds);
        var draws = new List<EnergyDraw>();
        var remaining = requiredEnergy;

        foreach (var source in candidates) {
            var available = GetEnergy(source, energyOverrides);
            if (available <= 0)
                continue;

            var amount = Math.Min(available, remaining);
            draws.Add(new EnergyDraw(source, amount));
            remaining -= amount;
            if (remaining <= 0)
                break;
        }

        return remaining > 0
            ? EnergyAllocationResult.Failure("Not enough energy to satisfy the spawn intent.")
            : EnergyAllocationResult.CreateSuccess(draws);
    }

    private static IEnumerable<RoomObjectSnapshot> EnumerateCandidates(
        IReadOnlyDictionary<string, RoomObjectSnapshot> roomObjects,
        RoomObjectSnapshot spawn,
        IReadOnlyList<string>? preferredStructureIds)
    {
        var emitted = new HashSet<string>(StringComparer.Ordinal);

        if (preferredStructureIds is not null) {
            foreach (var structureId in preferredStructureIds) {
                if (string.IsNullOrWhiteSpace(structureId))
                    continue;

                if (emitted.Contains(structureId))
                    continue;

                if (!roomObjects.TryGetValue(structureId, out var obj)) continue;

                emitted.Add(structureId);
                yield return obj;
            }
        }

        if (emitted.Add(spawn.Id))
            yield return spawn;

        foreach (var obj in roomObjects.Values) {
            if (emitted.Contains(obj.Id))
                continue;

            if (!string.Equals(obj.RoomName, spawn.RoomName, StringComparison.Ordinal))
                continue;

            if (!string.Equals(obj.UserId, spawn.UserId, StringComparison.Ordinal))
                continue;

            if (obj.Type is not (RoomObjectTypes.Spawn or RoomObjectTypes.Extension))
                continue;

            emitted.Add(obj.Id);
            yield return obj;
        }
    }

    private static int GetEnergy(RoomObjectSnapshot obj, IReadOnlyDictionary<string, int>? overrides)
    {
        return overrides is not null && overrides.TryGetValue(obj.Id, out var remaining)
            ? remaining
            : obj.Store.GetValueOrDefault(RoomDocumentFields.RoomObject.Store.Energy, 0);
    }
}

internal sealed record EnergyAllocationResult(bool Success, string? Error, IReadOnlyList<EnergyDraw> Draws)
{
    public static EnergyAllocationResult Failure(string error)
        => new(false, error, []);

    public static EnergyAllocationResult CreateSuccess(IReadOnlyList<EnergyDraw> draws)
        => new(true, null, draws);
}

internal sealed record EnergyDraw(RoomObjectSnapshot Source, int Amount);
