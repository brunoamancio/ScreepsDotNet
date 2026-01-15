namespace ScreepsDotNet.Common.Constants;

using System.Collections.Generic;

public static class ScreepsGameConstants
{
    public const int MaxCreepBodyParts = 50;
    public const int CreepSpawnTime = 3;
    public const int CarryCapacity = 50;
    public const int BodyPartHitPoints = 100;
    public const int CreepLifeTime = 1500;
    public const int CreepClaimLifeTime = 600;
    public const double SpawnRenewRatio = 1.2;
    public const int SpawnHits = 5000;
    public const int SpawnEnergyCapacity = 300;
    public const int ExtensionHits = 1000;

    public static IReadOnlyDictionary<string, int> BodyPartCost { get; } = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["move"] = 50,
        ["work"] = 100,
        ["attack"] = 80,
        ["carry"] = 50,
        ["heal"] = 250,
        ["ranged_attack"] = 150,
        ["tough"] = 10,
        ["claim"] = 600
    };
}
