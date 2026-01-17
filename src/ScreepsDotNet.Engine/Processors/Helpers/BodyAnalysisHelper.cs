namespace ScreepsDotNet.Engine.Processors.Helpers;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;

internal interface IBodyAnalysisHelper
{
    BodyAnalysisResult Analyze(IReadOnlyList<BodyPartType> bodyParts);
}

internal sealed class BodyAnalysisHelper : IBodyAnalysisHelper
{
    public BodyAnalysisResult Analyze(IReadOnlyList<BodyPartType> bodyParts)
    {
        if (bodyParts.Count == 0)
            return BodyAnalysisResult.Failure("Body parts are required.");

        if (bodyParts.Count > ScreepsGameConstants.MaxCreepBodyParts)
            return BodyAnalysisResult.Failure($"Body size exceeds {ScreepsGameConstants.MaxCreepBodyParts} parts.");

        var normalized = new BodyPartType[bodyParts.Count];
        var counts = new Dictionary<BodyPartType, int>();
        var totalCost = 0;
        var carryCapacity = 0;

        for (var i = 0; i < bodyParts.Count; i++) {
            var partType = bodyParts[i];
            normalized[i] = partType;

            if (!ScreepsGameConstants.TryGetBodyPartEnergyCost(partType, out var cost))
                return BodyAnalysisResult.Failure($"Unknown body part cost for '{partType}'.");

            totalCost += cost;
            counts[partType] = counts.TryGetValue(partType, out var existing) ? existing + 1 : 1;

            if (partType == BodyPartType.Carry)
                carryCapacity += ScreepsGameConstants.CarryCapacity;
        }

        var spawnTime = bodyParts.Count * ScreepsGameConstants.CreepSpawnTime;
        var totalHits = bodyParts.Count * ScreepsGameConstants.BodyPartHitPoints;

        return BodyAnalysisResult.CreateSuccess(normalized, counts, totalCost, totalHits, carryCapacity, spawnTime);
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
        => new(false, error, [], new Dictionary<BodyPartType, int>(), 0, 0, 0, 0);

    public static BodyAnalysisResult CreateSuccess(
        IReadOnlyList<BodyPartType> bodyParts,
        IReadOnlyDictionary<BodyPartType, int> partCounts,
        int totalEnergyCost,
        int totalHits,
        int carryCapacity,
        int spawnTime)
        => new(true, null, bodyParts, partCounts, totalEnergyCost, totalHits, carryCapacity, spawnTime);
}
