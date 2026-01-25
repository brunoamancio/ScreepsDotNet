namespace ScreepsDotNet.Common.Constants;

/// <summary>
/// Lab reaction formulas and cooldown times.
/// Ported from Node.js REACTIONS and REACTION_TIME constants.
/// </summary>
public static class LabReactions
{
    private static readonly StringComparer Comparer = StringComparer.Ordinal;

    /// <summary>
    /// Reaction recipes mapping reagent1 + reagent2 → product.
    /// Access via Recipes[reagent1][reagent2] to get the product mineral type.
    /// Total: 62 reaction formulas.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Recipes { get; } =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(Comparer)
        {
            [ResourceTypes.Hydrogen] = new Dictionary<string, string>(Comparer)
            {
                [ResourceTypes.Oxygen] = ResourceTypes.Hydroxide,
                [ResourceTypes.Lemergium] = ResourceTypes.LemergiumHydride,
                [ResourceTypes.Keanium] = ResourceTypes.KeaniumHydride,
                [ResourceTypes.Utrium] = ResourceTypes.UtriumHydride,
                [ResourceTypes.Zynthium] = ResourceTypes.ZynthiumHydride,
                [ResourceTypes.Ghodium] = ResourceTypes.GhodiumHydride
            },
            [ResourceTypes.Oxygen] = new Dictionary<string, string>(Comparer)
            {
                [ResourceTypes.Hydrogen] = ResourceTypes.Hydroxide,
                [ResourceTypes.Lemergium] = ResourceTypes.LemergiumOxide,
                [ResourceTypes.Keanium] = ResourceTypes.KeaniumOxide,
                [ResourceTypes.Utrium] = ResourceTypes.UtriumOxide,
                [ResourceTypes.Zynthium] = ResourceTypes.ZynthiumOxide,
                [ResourceTypes.Ghodium] = ResourceTypes.GhodiumOxide
            },
            [ResourceTypes.Zynthium] = new Dictionary<string, string>(Comparer)
            {
                [ResourceTypes.Keanium] = ResourceTypes.ZynthiumKeanite,
                [ResourceTypes.Hydrogen] = ResourceTypes.ZynthiumHydride,
                [ResourceTypes.Oxygen] = ResourceTypes.ZynthiumOxide
            },
            [ResourceTypes.Lemergium] = new Dictionary<string, string>(Comparer)
            {
                [ResourceTypes.Utrium] = ResourceTypes.UtriumLemergite,
                [ResourceTypes.Hydrogen] = ResourceTypes.LemergiumHydride,
                [ResourceTypes.Oxygen] = ResourceTypes.LemergiumOxide
            },
            [ResourceTypes.Keanium] = new Dictionary<string, string>(Comparer)
            {
                [ResourceTypes.Zynthium] = ResourceTypes.ZynthiumKeanite,
                [ResourceTypes.Hydrogen] = ResourceTypes.KeaniumHydride,
                [ResourceTypes.Oxygen] = ResourceTypes.KeaniumOxide
            },
            [ResourceTypes.Ghodium] = new Dictionary<string, string>(Comparer)
            {
                [ResourceTypes.Hydrogen] = ResourceTypes.GhodiumHydride,
                [ResourceTypes.Oxygen] = ResourceTypes.GhodiumOxide
            },
            [ResourceTypes.Utrium] = new Dictionary<string, string>(Comparer)
            {
                [ResourceTypes.Lemergium] = ResourceTypes.UtriumLemergite,
                [ResourceTypes.Hydrogen] = ResourceTypes.UtriumHydride,
                [ResourceTypes.Oxygen] = ResourceTypes.UtriumOxide
            },
            [ResourceTypes.Hydroxide] = new Dictionary<string, string>(Comparer)
            {
                [ResourceTypes.UtriumHydride] = ResourceTypes.UtriumAcid,
                [ResourceTypes.UtriumOxide] = ResourceTypes.UtriumAlkalide,
                [ResourceTypes.ZynthiumHydride] = ResourceTypes.ZynthiumAcid,
                [ResourceTypes.ZynthiumOxide] = ResourceTypes.ZynthiumAlkalide,
                [ResourceTypes.KeaniumHydride] = ResourceTypes.KeaniumAcid,
                [ResourceTypes.KeaniumOxide] = ResourceTypes.KeaniumAlkalide,
                [ResourceTypes.LemergiumHydride] = ResourceTypes.LemergiumAcid,
                [ResourceTypes.LemergiumOxide] = ResourceTypes.LemergiumAlkalide,
                [ResourceTypes.GhodiumHydride] = ResourceTypes.GhodiumAcid,
                [ResourceTypes.GhodiumOxide] = ResourceTypes.GhodiumAlkalide
            },
            [ResourceTypes.Catalyst] = new Dictionary<string, string>(Comparer)
            {
                [ResourceTypes.UtriumAcid] = ResourceTypes.CatalyzedUtriumAcid,
                [ResourceTypes.UtriumAlkalide] = ResourceTypes.CatalyzedUtriumAlkalide,
                [ResourceTypes.LemergiumAcid] = ResourceTypes.CatalyzedLemergiumAcid,
                [ResourceTypes.LemergiumAlkalide] = ResourceTypes.CatalyzedLemergiumAlkalide,
                [ResourceTypes.KeaniumAcid] = ResourceTypes.CatalyzedKeaniumAcid,
                [ResourceTypes.KeaniumAlkalide] = ResourceTypes.CatalyzedKeaniumAlkalide,
                [ResourceTypes.ZynthiumAcid] = ResourceTypes.CatalyzedZynthiumAcid,
                [ResourceTypes.ZynthiumAlkalide] = ResourceTypes.CatalyzedZynthiumAlkalide,
                [ResourceTypes.GhodiumAcid] = ResourceTypes.CatalyzedGhodiumAcid,
                [ResourceTypes.GhodiumAlkalide] = ResourceTypes.CatalyzedGhodiumAlkalide
            },
            [ResourceTypes.ZynthiumKeanite] = new Dictionary<string, string>(Comparer)
            {
                [ResourceTypes.UtriumLemergite] = ResourceTypes.Ghodium
            },
            [ResourceTypes.UtriumLemergite] = new Dictionary<string, string>(Comparer)
            {
                [ResourceTypes.ZynthiumKeanite] = ResourceTypes.Ghodium
            },
            [ResourceTypes.LemergiumHydride] = new Dictionary<string, string>(Comparer)
            {
                [ResourceTypes.Hydroxide] = ResourceTypes.LemergiumAcid
            },
            [ResourceTypes.ZynthiumHydride] = new Dictionary<string, string>(Comparer)
            {
                [ResourceTypes.Hydroxide] = ResourceTypes.ZynthiumAcid
            },
            [ResourceTypes.GhodiumHydride] = new Dictionary<string, string>(Comparer)
            {
                [ResourceTypes.Hydroxide] = ResourceTypes.GhodiumAcid
            },
            [ResourceTypes.KeaniumHydride] = new Dictionary<string, string>(Comparer)
            {
                [ResourceTypes.Hydroxide] = ResourceTypes.KeaniumAcid
            },
            [ResourceTypes.UtriumHydride] = new Dictionary<string, string>(Comparer)
            {
                [ResourceTypes.Hydroxide] = ResourceTypes.UtriumAcid
            },
            [ResourceTypes.LemergiumOxide] = new Dictionary<string, string>(Comparer)
            {
                [ResourceTypes.Hydroxide] = ResourceTypes.LemergiumAlkalide
            },
            [ResourceTypes.ZynthiumOxide] = new Dictionary<string, string>(Comparer)
            {
                [ResourceTypes.Hydroxide] = ResourceTypes.ZynthiumAlkalide
            },
            [ResourceTypes.KeaniumOxide] = new Dictionary<string, string>(Comparer)
            {
                [ResourceTypes.Hydroxide] = ResourceTypes.KeaniumAlkalide
            },
            [ResourceTypes.UtriumOxide] = new Dictionary<string, string>(Comparer)
            {
                [ResourceTypes.Hydroxide] = ResourceTypes.UtriumAlkalide
            },
            [ResourceTypes.GhodiumOxide] = new Dictionary<string, string>(Comparer)
            {
                [ResourceTypes.Hydroxide] = ResourceTypes.GhodiumAlkalide
            },
            [ResourceTypes.LemergiumAcid] = new Dictionary<string, string>(Comparer)
            {
                [ResourceTypes.Catalyst] = ResourceTypes.CatalyzedLemergiumAcid
            },
            [ResourceTypes.KeaniumAcid] = new Dictionary<string, string>(Comparer)
            {
                [ResourceTypes.Catalyst] = ResourceTypes.CatalyzedKeaniumAcid
            },
            [ResourceTypes.ZynthiumAcid] = new Dictionary<string, string>(Comparer)
            {
                [ResourceTypes.Catalyst] = ResourceTypes.CatalyzedZynthiumAcid
            },
            [ResourceTypes.UtriumAcid] = new Dictionary<string, string>(Comparer)
            {
                [ResourceTypes.Catalyst] = ResourceTypes.CatalyzedUtriumAcid
            },
            [ResourceTypes.GhodiumAcid] = new Dictionary<string, string>(Comparer)
            {
                [ResourceTypes.Catalyst] = ResourceTypes.CatalyzedGhodiumAcid
            },
            [ResourceTypes.LemergiumAlkalide] = new Dictionary<string, string>(Comparer)
            {
                [ResourceTypes.Catalyst] = ResourceTypes.CatalyzedLemergiumAlkalide
            },
            [ResourceTypes.UtriumAlkalide] = new Dictionary<string, string>(Comparer)
            {
                [ResourceTypes.Catalyst] = ResourceTypes.CatalyzedUtriumAlkalide
            },
            [ResourceTypes.KeaniumAlkalide] = new Dictionary<string, string>(Comparer)
            {
                [ResourceTypes.Catalyst] = ResourceTypes.CatalyzedKeaniumAlkalide
            },
            [ResourceTypes.ZynthiumAlkalide] = new Dictionary<string, string>(Comparer)
            {
                [ResourceTypes.Catalyst] = ResourceTypes.CatalyzedZynthiumAlkalide
            },
            [ResourceTypes.GhodiumAlkalide] = new Dictionary<string, string>(Comparer)
            {
                [ResourceTypes.Catalyst] = ResourceTypes.CatalyzedGhodiumAlkalide
            }
        };

    /// <summary>
    /// Cooldown times (in ticks) for each reaction product.
    /// The lab will be on cooldown for this duration after producing the compound.
    /// </summary>
    public static IReadOnlyDictionary<string, int> CooldownTimes { get; } =
        new Dictionary<string, int>(Comparer)
        {
            [ResourceTypes.Hydroxide] = 20,
            [ResourceTypes.ZynthiumKeanite] = 5,
            [ResourceTypes.UtriumLemergite] = 5,
            [ResourceTypes.Ghodium] = 5,
            [ResourceTypes.UtriumHydride] = 10,
            [ResourceTypes.UtriumAcid] = 5,
            [ResourceTypes.CatalyzedUtriumAcid] = 60,
            [ResourceTypes.UtriumOxide] = 10,
            [ResourceTypes.UtriumAlkalide] = 5,
            [ResourceTypes.CatalyzedUtriumAlkalide] = 60,
            [ResourceTypes.KeaniumHydride] = 10,
            [ResourceTypes.KeaniumAcid] = 5,
            [ResourceTypes.CatalyzedKeaniumAcid] = 60,
            [ResourceTypes.KeaniumOxide] = 10,
            [ResourceTypes.KeaniumAlkalide] = 5,
            [ResourceTypes.CatalyzedKeaniumAlkalide] = 60,
            [ResourceTypes.LemergiumHydride] = 15,
            [ResourceTypes.LemergiumAcid] = 10,
            [ResourceTypes.CatalyzedLemergiumAcid] = 65,
            [ResourceTypes.LemergiumOxide] = 10,
            [ResourceTypes.LemergiumAlkalide] = 5,
            [ResourceTypes.CatalyzedLemergiumAlkalide] = 60,
            [ResourceTypes.ZynthiumHydride] = 20,
            [ResourceTypes.ZynthiumAcid] = 40,
            [ResourceTypes.CatalyzedZynthiumAcid] = 160,
            [ResourceTypes.ZynthiumOxide] = 10,
            [ResourceTypes.ZynthiumAlkalide] = 5,
            [ResourceTypes.CatalyzedZynthiumAlkalide] = 60,
            [ResourceTypes.GhodiumHydride] = 10,
            [ResourceTypes.GhodiumAcid] = 15,
            [ResourceTypes.CatalyzedGhodiumAcid] = 80,
            [ResourceTypes.GhodiumOxide] = 10,
            [ResourceTypes.GhodiumAlkalide] = 30,
            [ResourceTypes.CatalyzedGhodiumAlkalide] = 150
        };

    /// <summary>
    /// Attempts to look up the reaction product for two reagents.
    /// </summary>
    /// <param name="reagent1">First reagent mineral type.</param>
    /// <param name="reagent2">Second reagent mineral type.</param>
    /// <param name="product">The resulting product mineral type if reaction exists.</param>
    /// <returns>True if a reaction exists for the given reagents, false otherwise.</returns>
    public static bool TryGetProduct(string reagent1, string reagent2, out string product)
    {
        product = string.Empty;

        if (!Recipes.TryGetValue(reagent1, out var reagent2Map))
            return false;

        if (!reagent2Map.TryGetValue(reagent2, out var result))
            return false;

        product = result;
        return true;
    }

    /// <summary>
    /// Attempts to look up the reagents for a given product (reverse reaction).
    /// </summary>
    /// <param name="product">The product mineral type to decompose.</param>
    /// <param name="reagent1">First reagent mineral type if decomposition exists.</param>
    /// <param name="reagent2">Second reagent mineral type if decomposition exists.</param>
    /// <returns>True if the product can be decomposed, false otherwise.</returns>
    public static bool TryGetReagents(string product, out string reagent1, out string reagent2)
    {
        reagent1 = string.Empty;
        reagent2 = string.Empty;

        foreach (var (r1, reagent2Map) in Recipes) {
            foreach (var (r2, p) in reagent2Map) {
                if (!string.Equals(p, product, StringComparison.Ordinal)) continue;

                reagent1 = r1;
                reagent2 = r2;
                return true;
            }
        }

        return false;
    }
}
