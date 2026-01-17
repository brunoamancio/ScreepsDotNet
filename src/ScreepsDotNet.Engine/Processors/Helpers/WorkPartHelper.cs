namespace ScreepsDotNet.Engine.Processors.Helpers;

using System;
using System.Collections.Generic;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;

internal static class WorkPartHelper
{
    private static readonly IReadOnlyDictionary<string, WorkBoostProfile> WorkBoostTables =
        new Dictionary<string, WorkBoostProfile>(StringComparer.Ordinal)
        {
            [ResourceTypes.LemergiumHydride] = new(BuildMultiplier: 1.5, RepairMultiplier: 1.5),
            [ResourceTypes.LemergiumAcid] = new(BuildMultiplier: 1.8, RepairMultiplier: 1.8),
            [ResourceTypes.CatalyzedLemergiumAcid] = new(BuildMultiplier: 2.0, RepairMultiplier: 2.0),
            [ResourceTypes.UtriumOxide] = new(HarvestMultiplier: 3.0),
            [ResourceTypes.UtriumAlkalide] = new(HarvestMultiplier: 5.0),
            [ResourceTypes.CatalyzedUtriumAlkalide] = new(HarvestMultiplier: 7.0)
        };

    public static bool TryGetActiveWorkParts(RoomObjectSnapshot creep, out List<CreepBodyPartSnapshot> parts)
    {
        parts = [];
        foreach (var part in creep.Body)
        {
            if (part.Type == BodyPartType.Work && part.Hits > 0)
                parts.Add(part);
        }

        return parts.Count > 0;
    }

    public static double ApplyWorkBoosts(
        IReadOnlyList<CreepBodyPartSnapshot> parts,
        double baseEffect,
        WorkBoostEffect effect,
        int perPartPower)
    {
        if (parts.Count == 0)
            return Math.Floor(baseEffect);

        double additional = 0;
        foreach (var part in parts)
        {
            if (part.Type != BodyPartType.Work || part.Hits <= 0)
                continue;

            if (string.IsNullOrWhiteSpace(part.Boost))
                continue;

            if (!WorkBoostTables.TryGetValue(part.Boost!, out var profile))
                continue;

            var multiplier = effect switch
            {
                WorkBoostEffect.Repair => profile.RepairMultiplier,
                WorkBoostEffect.Build => profile.BuildMultiplier,
                WorkBoostEffect.Harvest => profile.HarvestMultiplier,
                _ => 1.0
            };

            if (multiplier <= 1.0)
                continue;

            additional += (multiplier - 1.0) * perPartPower;
        }

        return Math.Floor(baseEffect + additional);
    }

    private sealed record WorkBoostProfile(
        double BuildMultiplier = 1.0,
        double RepairMultiplier = 1.0,
        double HarvestMultiplier = 1.0);
}

internal enum WorkBoostEffect
{
    Build,
    Repair,
    Harvest
}
