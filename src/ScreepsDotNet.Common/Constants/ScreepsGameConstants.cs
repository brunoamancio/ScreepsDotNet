namespace ScreepsDotNet.Common.Constants;

using System.Collections.Generic;
using ScreepsDotNet.Common.Extensions;
using ScreepsDotNet.Common.Types;

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
    public const int SpawnInitialEnergy = SpawnEnergyCapacity;
    public const int ExtensionHits = 1000;

    public static IReadOnlyDictionary<BodyPartType, int> BodyPartEnergyCost { get; } = new Dictionary<BodyPartType, int>
    {
        [BodyPartType.Move] = 50,
        [BodyPartType.Work] = 100,
        [BodyPartType.Attack] = 80,
        [BodyPartType.Carry] = 50,
        [BodyPartType.Heal] = 250,
        [BodyPartType.RangedAttack] = 150,
        [BodyPartType.Tough] = 10,
        [BodyPartType.Claim] = 600
    };

    public static bool TryGetBodyPartEnergyCost(BodyPartType bodyPartType, out int cost)
        => BodyPartEnergyCost.TryGetValue(bodyPartType, out cost);

    public static bool TryGetBodyPartEnergyCost(string documentValue, out int cost)
    {
        if (!documentValue.TryParseBodyPartType(out var bodyPartType))
        {
            cost = default;
            return false;
        }

        return BodyPartEnergyCost.TryGetValue(bodyPartType, out cost);
    }
}
