using ScreepsDotNet.Common.Constants;

namespace ScreepsDotNet.Engine.Validation.Constants;

/// <summary>
/// Resource cost and capacity requirements for intent validation.
/// Extracted from Node.js engine constants and intent processors.
/// </summary>
public static class ResourceRequirements
{
    // Lab resource costs (from ScreepsGameConstants)
    public const int LabBoostEnergyCost = 20;
    public const int LabBoostMineralCost = 30;
    public const int LabUnboostEnergyCost = 0;
    public const int LabUnboostMineralCost = 15;

    // Power spawn costs
    public const int PowerSpawnPowerCost = 1;
    public const int PowerSpawnEnergyCost = 50;

    // Tower action costs
    public const int TowerEnergyCost = 10;

    /// <summary>
    /// All valid resource types that can be transferred/stored.
    /// Used for schema validation of resourceType field.
    /// </summary>
    public static readonly IReadOnlySet<string> AllResourceTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        // Energy and Power
        ResourceTypes.Energy,
        ResourceTypes.Power,

        // Base Minerals
        ResourceTypes.Hydrogen,
        ResourceTypes.Oxygen,
        ResourceTypes.Utrium,
        ResourceTypes.Keanium,
        ResourceTypes.Lemergium,
        ResourceTypes.Zynthium,
        ResourceTypes.Catalyst,
        ResourceTypes.Ghodium,

        // Tier 1 Compounds
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

        // Tier 2 Boosts
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

        // Tier 3 Boosts
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

        // Power Creep Resources
        ResourceTypes.Ops,

        // Deposit Resources
        ResourceTypes.Silicon,
        ResourceTypes.Metal,
        ResourceTypes.Biomass,
        ResourceTypes.Mist,

        // Commodities (Base)
        ResourceTypes.UtriumBar,
        ResourceTypes.LemergiumBar,
        ResourceTypes.ZynthiumBar,
        ResourceTypes.KeaniumBar,
        ResourceTypes.GhodiumMelt,
        ResourceTypes.Oxidant,
        ResourceTypes.Reductant,
        ResourceTypes.Purifier,
        ResourceTypes.Battery,

        // Commodities (Electronics)
        ResourceTypes.Composite,
        ResourceTypes.Crystal,
        ResourceTypes.Liquid,
        ResourceTypes.Wire,
        ResourceTypes.Switch,
        ResourceTypes.Transistor,
        ResourceTypes.Microchip,
        ResourceTypes.Circuit,
        ResourceTypes.Device,

        // Commodities (Biologics)
        ResourceTypes.Cell,
        ResourceTypes.Phlegm,
        ResourceTypes.Tissue,
        ResourceTypes.Muscle,
        ResourceTypes.Organoid,
        ResourceTypes.Organism,

        // Commodities (Mechanicals)
        ResourceTypes.Alloy,
        ResourceTypes.Tube,
        ResourceTypes.Fixtures,
        ResourceTypes.Frame,
        ResourceTypes.Hydraulics,
        ResourceTypes.Machine,

        // Commodities (Mysticals)
        ResourceTypes.Condensate,
        ResourceTypes.Concentrate,
        ResourceTypes.Extract,
        ResourceTypes.Spirit,
        ResourceTypes.Emanation,
        ResourceTypes.Essence
    };

    /// <summary>
    /// Check if a resource type is valid for transfer/storage.
    /// </summary>
    public static bool IsValidResourceType(string resourceType)
        => AllResourceTypes.Contains(resourceType);
}
