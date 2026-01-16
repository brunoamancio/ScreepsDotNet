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
    public const double CreepCorpseRate = 0.2;
    public const int CreepPartMaxEnergy = 125;
    public const double SpawnRenewRatio = 1.2;
    public const int SpawnHits = 5000;
    public const int SpawnEnergyCapacity = 300;
    public const int SpawnInitialEnergy = SpawnEnergyCapacity;
    public const int ExtensionHits = 1000;
    public const int LabBoostEnergy = 20;
    public const int LabBoostMineral = 30;
    public const int TombstoneDecayPerPart = 5;
    public const int TombstoneDecayPowerCreep = 500;
    public static IReadOnlyList<string> ResourceOrder { get; } =
    [
        ResourceTypes.Energy,
        ResourceTypes.Power,
        ResourceTypes.Hydrogen,
        ResourceTypes.Oxygen,
        ResourceTypes.Utrium,
        ResourceTypes.Keanium,
        ResourceTypes.Lemergium,
        ResourceTypes.Zynthium,
        ResourceTypes.Catalyst,
        ResourceTypes.Ghodium,
        ResourceTypes.Hydroxide,
        ResourceTypes.ZynthiumKeanite,
        ResourceTypes.UtriumLemergite,
        ResourceTypes.UtriumHydride,
        ResourceTypes.UtriumOxide,
        ResourceTypes.KeaniumHydride,
        ResourceTypes.KeaniumOxide,
        ResourceTypes.LemergiumHydride,
        ResourceTypes.LemergiumOxide,
        ResourceTypes.ZynthiumHydride,
        ResourceTypes.ZynthiumOxide,
        ResourceTypes.GhodiumHydride,
        ResourceTypes.GhodiumOxide,
        ResourceTypes.UtriumAcid,
        ResourceTypes.UtriumAlkalide,
        ResourceTypes.KeaniumAcid,
        ResourceTypes.KeaniumAlkalide,
        ResourceTypes.LemergiumAcid,
        ResourceTypes.LemergiumAlkalide,
        ResourceTypes.ZynthiumAcid,
        ResourceTypes.ZynthiumAlkalide,
        ResourceTypes.GhodiumAcid,
        ResourceTypes.GhodiumAlkalide,
        ResourceTypes.CatalyzedUtriumAcid,
        ResourceTypes.CatalyzedUtriumAlkalide,
        ResourceTypes.CatalyzedKeaniumAcid,
        ResourceTypes.CatalyzedKeaniumAlkalide,
        ResourceTypes.CatalyzedLemergiumAcid,
        ResourceTypes.CatalyzedLemergiumAlkalide,
        ResourceTypes.CatalyzedZynthiumAcid,
        ResourceTypes.CatalyzedZynthiumAlkalide,
        ResourceTypes.CatalyzedGhodiumAcid,
        ResourceTypes.CatalyzedGhodiumAlkalide,
        ResourceTypes.Ops,
        ResourceTypes.Silicon,
        ResourceTypes.Metal,
        ResourceTypes.Biomass,
        ResourceTypes.Mist,
        ResourceTypes.UtriumBar,
        ResourceTypes.LemergiumBar,
        ResourceTypes.ZynthiumBar,
        ResourceTypes.KeaniumBar,
        ResourceTypes.GhodiumMelt,
        ResourceTypes.Oxidant,
        ResourceTypes.Reductant,
        ResourceTypes.Purifier,
        ResourceTypes.Battery,
        ResourceTypes.Composite,
        ResourceTypes.Crystal,
        ResourceTypes.Liquid,
        ResourceTypes.Wire,
        ResourceTypes.Switch,
        ResourceTypes.Transistor,
        ResourceTypes.Microchip,
        ResourceTypes.Circuit,
        ResourceTypes.Device,
        ResourceTypes.Cell,
        ResourceTypes.Phlegm,
        ResourceTypes.Tissue,
        ResourceTypes.Muscle,
        ResourceTypes.Organoid,
        ResourceTypes.Organism,
        ResourceTypes.Alloy,
        ResourceTypes.Tube,
        ResourceTypes.Fixtures,
        ResourceTypes.Frame,
        ResourceTypes.Hydraulics,
        ResourceTypes.Machine,
        ResourceTypes.Condensate,
        ResourceTypes.Concentrate,
        ResourceTypes.Extract,
        ResourceTypes.Spirit,
        ResourceTypes.Emanation,
        ResourceTypes.Essence
    ];

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
