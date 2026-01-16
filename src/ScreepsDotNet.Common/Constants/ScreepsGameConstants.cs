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
    public const int RepairPower = 100;
    public const int BuildPower = 5;
    public const double RepairEnergyCost = 0.01;
    public const int HarvestPower = 2;
    public const int HarvestMineralPower = 1;
    public const int HarvestDepositPower = 1;
    public const double DepositExhaustMultiply = 0.001;
    public const double DepositExhaustPow = 1.2;
    public const int DepositDecayTime = 50_000;
    public const int TombstoneDecayPerPart = 5;
    public const int TombstoneDecayPowerCreep = 500;
    public const int TowerEnergyCost = 10;
    public const int TowerPowerAttack = 600;
    public const int TowerPowerHeal = 400;
    public const int TowerPowerRepair = 800;
    public const int TowerOptimalRange = 5;
    public const int TowerFalloffRange = 20;
    public const double TowerFalloff = 0.75;
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

    public static IReadOnlyDictionary<int, int> ExtensionEnergyCapacityByControllerLevel { get; } =
        new Dictionary<int, int>
        {
            [0] = 50,
            [1] = 50,
            [2] = 50,
            [3] = 50,
            [4] = 50,
            [5] = 50,
            [6] = 50,
            [7] = 100,
            [8] = 200
        };

    public const int TowerHits = 3000;
    public const int TowerCapacity = 1000;
    public const int ObserverHits = 500;
    public const int PowerSpawnHits = 5000;
    public const int PowerSpawnEnergyCapacity = 5000;
    public const int PowerSpawnPowerCapacity = 100;
    public const int ExtractorHits = 500;
    public const int ExtractorCooldown = 5;
    public const int LabHits = 500;
    public const int LabEnergyCapacity = 2000;
    public const int LabMineralCapacity = 3000;
    public const int LinkHits = 1000;
    public const int LinkHitsMax = 1000;
    public const int LinkCapacity = 800;
    public const int LinkCooldown = 1;
    public const int StorageHits = 10000;
    public const int StorageCapacity = 1_000_000;
    public const int TerminalHits = 3000;
    public const int TerminalCapacity = 300_000;
    public const int TerminalCooldown = 10;
    public const int ContainerHits = 250_000;
    public const int ContainerCapacity = 2_000;
    public const int ContainerDecayAmount = 5_000;
    public const int ContainerDecayInterval = 100;
    public const int ContainerDecayOwnedInterval = 500;
    public const int RoadHits = 5_000;
    public const int RoadDecayAmount = 100;
    public const int RoadDecayInterval = 1_000;
    public const int RoadSwampMultiplier = 5;
    public const int RoadWallMultiplier = 150;
    public const int RampartHits = 1;
    public const int RampartDecayAmount = 300;
    public const int RampartDecayInterval = 100;
    public static IReadOnlyDictionary<int, int> RampartHitsMaxByControllerLevel { get; } =
        new Dictionary<int, int>
        {
            [2] = 300_000,
            [3] = 1_000_000,
            [4] = 3_000_000,
            [5] = 10_000_000,
            [6] = 30_000_000,
            [7] = 100_000_000,
            [8] = 300_000_000
        };
    public const int WallHits = 1;
    public const int WallHitsMax = 300_000_000;
    public const int NukerHits = 1000;
    public const int NukerEnergyCapacity = 300_000;
    public const int NukerGhodiumCapacity = 5_000;
    public const int NukerCooldown = 100_000;
    public const int FactoryHits = 1000;
    public const int FactoryCapacity = 50_000;
    public const int TerrainMaskWall = 1;
    public const int TerrainMaskSwamp = 2;

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
