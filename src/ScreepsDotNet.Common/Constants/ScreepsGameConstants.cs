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
    public const int LabReactionAmount = 5;
    public const int LabUnboostEnergy = 0;
    public const int LabUnboostMineral = 15;
    public const int RepairPower = 100;
    public const int BuildPower = 5;
    public const double RepairEnergyCost = 0.01;
    public const int DismantlePower = 50;
    public const double DismantleCost = 0.005;
    public const int HarvestPower = 2;
    public const int HarvestMineralPower = 1;
    public const int HarvestDepositPower = 1;
    public const double DepositExhaustMultiply = 0.001;
    public const double DepositExhaustPow = 1.2;
    public const int DepositDecayTime = 50_000;
    public const int EnergyDecay = 1000;
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

    public static IReadOnlyList<string> IntershardResources { get; } =
    [
        ResourceTypes.SubscriptionToken,
        ResourceTypes.CpuUnlock,
        ResourceTypes.Pixel,
        ResourceTypes.AccessKey
    ];

    public static IReadOnlySet<string> ResourcesAll { get; } = new HashSet<string>(ResourceOrder.Concat(IntershardResources), StringComparer.Ordinal);

    public static IReadOnlyDictionary<ControllerLevel, int> ExtensionEnergyCapacityByControllerLevel { get; } =
        new Dictionary<ControllerLevel, int>
        {
            [ControllerLevel.Level0] = 50,
            [ControllerLevel.Level1] = 50,
            [ControllerLevel.Level2] = 50,
            [ControllerLevel.Level3] = 50,
            [ControllerLevel.Level4] = 50,
            [ControllerLevel.Level5] = 50,
            [ControllerLevel.Level6] = 50,
            [ControllerLevel.Level7] = 100,
            [ControllerLevel.Level8] = 200
        };

    public const int TowerHits = 3000;
    public const int TowerCapacity = 1000;
    public const int ObserverHits = 500;
    public const int ObserverRange = 10;
    public const int PowerSpawnHits = 5000;
    public const int PowerSpawnEnergyCapacity = 5000;
    public const int PowerSpawnPowerCapacity = 100;
    public const int PowerSpawnEnergyRatio = 50;
    public const int ExtractorHits = 500;
    public const int ExtractorCooldown = 5;
    public const int LabHits = 500;
    public const int LabEnergyCapacity = 2000;
    public const int LabMineralCapacity = 3000;
    public const int LinkHits = 1000;
    public const int LinkHitsMax = 1000;
    public const int LinkCapacity = 800;
    public const int LinkCooldown = 1;
    public const double LinkLossRatio = 0.03;
    public const int StorageHits = 10000;
    public const int StorageCapacity = 1_000_000;
    public const int TerminalHits = 3000;
    public const int TerminalCapacity = 300_000;
    public const int TerminalCooldown = 10;
    public const double MarketFee = 0.05;
    public const int PowerLevelMultiply = 1000;
    public const double PowerLevelPow = 2.0;
    public const int PowerCreepMaxLevel = 25;
    public const int PowerCreepLifeTime = 5000;
    public const int PowerCreepDeleteCooldownMilliseconds = 24 * 60 * 60 * 1000;
    public const int PowerExperimentationCooldownMilliseconds = 24 * 60 * 60 * 1000;
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
    public static IReadOnlyDictionary<ControllerLevel, int> RampartHitsMaxByControllerLevel { get; } =
        new Dictionary<ControllerLevel, int>
        {
            [ControllerLevel.Level2] = 300_000,
            [ControllerLevel.Level3] = 1_000_000,
            [ControllerLevel.Level4] = 3_000_000,
            [ControllerLevel.Level5] = 10_000_000,
            [ControllerLevel.Level6] = 30_000_000,
            [ControllerLevel.Level7] = 100_000_000,
            [ControllerLevel.Level8] = 300_000_000
        };
    public const int WallHits = 1;
    public const int WallHitsMax = 300_000_000;
    public const int NukerHits = 1000;
    public const int NukerEnergyCapacity = 300_000;
    public const int NukerGhodiumCapacity = 5_000;
    public const int NukerCooldown = 100_000;
    public const int NukeRange = 10;
    public const int NukeLandTime = 50_000;
    public const int NukeDamageCenter = 10_000_000;
    public const int NukeDamageOuter = 5_000_000;
    public const int ControllerNukeBlockedUpgrade = 200;
    public const int FactoryHits = 1000;
    public const int FactoryCapacity = 50_000;
    public const int TerrainMaskWall = 1;
    public const int TerrainMaskSwamp = 2;

    // Source energy regeneration
    public const int EnergyRegenTime = 300;
    public const int SourceEnergyCapacity = 3000;
    public const int SourceEnergyNeutralCapacity = 1500;
    public const int SourceEnergyKeeperCapacity = 4000;

    // Mineral regeneration
    public const int MineralRegenTime = 50_000;
    public const double MineralDensityChange = 0.05;

    public static IReadOnlyDictionary<int, int> MineralDensityAmounts { get; } =
        new Dictionary<int, int>
        {
            [1] = 15_000,    // DENSITY_LOW
            [2] = 35_000,    // DENSITY_MODERATE
            [3] = 70_000,    // DENSITY_HIGH
            [4] = 100_000    // DENSITY_ULTRA
        };

    public static IReadOnlyDictionary<int, double> MineralDensityProbability { get; } =
        new Dictionary<int, double>
        {
            [1] = 0.1,  // DENSITY_LOW: 10%
            [2] = 0.5,  // DENSITY_MODERATE: 50%
            [3] = 0.9,  // DENSITY_HIGH: 90%
            [4] = 1.0   // DENSITY_ULTRA: 100%
        };

    public const int DensityLow = 1;
    public const int DensityModerate = 2;
    public const int DensityHigh = 3;
    public const int DensityUltra = 4;

    // Controller upgrade/downgrade
    public const int UpgradeControllerPower = 1;
    public const int ControllerMaxUpgradePerTick = 15;
    public const int ControllerDowngradeRestore = 100;
    public const int ControllerDowngradeSafeModeThreshold = 5000;

    // Controller reserve/claim
    public const int ControllerReserve = 1;
    public const int ControllerReserveMax = 5000;
    public const int ControllerClaimDowngrade = 300;
    public const int ControllerAttackBlockedUpgrade = 1000;

    // Controller level progression (energy required per level)
    public static IReadOnlyDictionary<ControllerLevel, int> ControllerLevelProgress { get; } =
        new Dictionary<ControllerLevel, int>
        {
            [ControllerLevel.Level1] = 200,
            [ControllerLevel.Level2] = 45_000,
            [ControllerLevel.Level3] = 135_000,
            [ControllerLevel.Level4] = 405_000,
            [ControllerLevel.Level5] = 1_215_000,
            [ControllerLevel.Level6] = 3_645_000,
            [ControllerLevel.Level7] = 10_935_000
        };

    // Controller downgrade timers (ticks before downgrade per level)
    public static IReadOnlyDictionary<ControllerLevel, int> ControllerDowngradeTimers { get; } =
        new Dictionary<ControllerLevel, int>
        {
            [ControllerLevel.Level1] = 20_000,
            [ControllerLevel.Level2] = 10_000,
            [ControllerLevel.Level3] = 20_000,
            [ControllerLevel.Level4] = 40_000,
            [ControllerLevel.Level5] = 80_000,
            [ControllerLevel.Level6] = 120_000,
            [ControllerLevel.Level7] = 150_000,
            [ControllerLevel.Level8] = 200_000
        };

    // Boost multipliers for WORK parts upgrading controller
    public static IReadOnlyDictionary<string, double> WorkBoostUpgradeMultipliers { get; } =
        new Dictionary<string, double>(StringComparer.Ordinal)
        {
            [ResourceTypes.GhodiumHydride] = 1.5,           // GH: +50% upgrade power
            [ResourceTypes.GhodiumAcid] = 1.8,              // GH2O: +80% upgrade power
            [ResourceTypes.CatalyzedGhodiumAcid] = 2.0      // XGH2O: +100% upgrade power
        };

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
        if (!documentValue.TryParseBodyPartType(out var bodyPartType)) {
            cost = default;
            return false;
        }

        return BodyPartEnergyCost.TryGetValue(bodyPartType, out cost);
    }
}
