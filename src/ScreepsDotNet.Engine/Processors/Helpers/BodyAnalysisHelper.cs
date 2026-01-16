namespace ScreepsDotNet.Engine.Processors.Helpers;

using System;
using System.Collections.Generic;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Extensions;
using ScreepsDotNet.Common.Types;

internal interface IBodyAnalysisHelper
{
    BodyAnalysisResult Analyze(IReadOnlyList<string> rawBodyParts);
}

internal sealed class BodyAnalysisHelper : IBodyAnalysisHelper
{
    public BodyAnalysisResult Analyze(IReadOnlyList<string> rawBodyParts)
    {
        if (rawBodyParts.Count == 0)
            return BodyAnalysisResult.Failure("Body parts are required.");

        if (rawBodyParts.Count > ScreepsGameConstants.MaxCreepBodyParts)
            return BodyAnalysisResult.Failure($"Body size exceeds {ScreepsGameConstants.MaxCreepBodyParts} parts.");

        var parsed = new BodyPartType[rawBodyParts.Count];
        var counts = new Dictionary<BodyPartType, int>();
        var totalCost = 0;
        var carryCapacity = 0;

        for (var i = 0; i < rawBodyParts.Count; i++)
        {
            var value = rawBodyParts[i];
            if (string.IsNullOrWhiteSpace(value) || !value.TryParseBodyPartType(out var partType))
                return BodyAnalysisResult.Failure($"Invalid body part '{value}'.");

            if (!ScreepsGameConstants.TryGetBodyPartEnergyCost(partType, out var cost))
                return BodyAnalysisResult.Failure($"Unknown body part cost for '{value}'.");

            parsed[i] = partType;
            totalCost += cost;
            counts[partType] = counts.TryGetValue(partType, out var existing) ? existing + 1 : 1;

            if (partType == BodyPartType.Carry)
                carryCapacity += ScreepsGameConstants.CarryCapacity;
        }

        var spawnTime = rawBodyParts.Count * ScreepsGameConstants.CreepSpawnTime;
        var totalHits = rawBodyParts.Count * ScreepsGameConstants.BodyPartHitPoints;

        return BodyAnalysisResult.CreateSuccess(parsed, counts, totalCost, totalHits, carryCapacity, spawnTime);
    }
}

internal sealed record BodyAnalysisResult(
    bool Success,
    string? Error,
    IReadOnlyList<BodyPartType> BodyParts,
    IReadOnlyDictionary<BodyPartType, int> PartCounts,
    int TotalEnergyCost,
    int TotalHits,
    int CarryCapacity,
    int SpawnTime)
{
    public static BodyAnalysisResult Failure(string error)
        => new(false, error, Array.Empty<BodyPartType>(), new Dictionary<BodyPartType, int>(), 0, 0, 0, 0);

    public static BodyAnalysisResult CreateSuccess(
        IReadOnlyList<BodyPartType> bodyParts,
        IReadOnlyDictionary<BodyPartType, int> partCounts,
        int totalEnergyCost,
        int totalHits,
        int carryCapacity,
        int spawnTime)
        => new(true, null, bodyParts, partCounts, totalEnergyCost, totalHits, carryCapacity, spawnTime);
}
