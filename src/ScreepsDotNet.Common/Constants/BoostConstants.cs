namespace ScreepsDotNet.Common.Constants;

using ScreepsDotNet.Common.Types;

/// <summary>
/// Boost multipliers for creep body parts.
/// Ported from Node.js BOOSTS constant.
/// Structure: BOOSTS[bodyPartType][mineralType][actionType] = multiplier
/// </summary>
public static class BoostConstants
{
    private static readonly StringComparer Comparer = StringComparer.Ordinal;

    /// <summary>
    /// Boost multipliers nested by body part type, mineral type, and action type.
    /// Access via Multipliers[BodyPartType.Work][ResourceTypes.GhodiumHydride][BoostActionTypes.UpgradeController] → 1.5
    /// Total: ~40 boost combinations across 7 body part types.
    /// </summary>
    public static IReadOnlyDictionary<BodyPartType, IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>>> Multipliers { get; } =
        new Dictionary<BodyPartType, IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>>>
        {
            [BodyPartType.Work] = new Dictionary<string, IReadOnlyDictionary<string, double>>(Comparer)
            {
                [ResourceTypes.UtriumOxide] = new Dictionary<string, double>(Comparer)
                {
                    [BoostActionTypes.Harvest] = 3
                },
                [ResourceTypes.UtriumAlkalide] = new Dictionary<string, double>(Comparer)
                {
                    [BoostActionTypes.Harvest] = 5
                },
                [ResourceTypes.CatalyzedUtriumAlkalide] = new Dictionary<string, double>(Comparer)
                {
                    [BoostActionTypes.Harvest] = 7
                },
                [ResourceTypes.LemergiumHydride] = new Dictionary<string, double>(Comparer)
                {
                    [BoostActionTypes.Build] = 1.5,
                    [BoostActionTypes.Repair] = 1.5
                },
                [ResourceTypes.LemergiumAcid] = new Dictionary<string, double>(Comparer)
                {
                    [BoostActionTypes.Build] = 1.8,
                    [BoostActionTypes.Repair] = 1.8
                },
                [ResourceTypes.CatalyzedLemergiumAcid] = new Dictionary<string, double>(Comparer)
                {
                    [BoostActionTypes.Build] = 2,
                    [BoostActionTypes.Repair] = 2
                },
                [ResourceTypes.ZynthiumHydride] = new Dictionary<string, double>(Comparer)
                {
                    [BoostActionTypes.Dismantle] = 2
                },
                [ResourceTypes.ZynthiumAcid] = new Dictionary<string, double>(Comparer)
                {
                    [BoostActionTypes.Dismantle] = 3
                },
                [ResourceTypes.CatalyzedZynthiumAcid] = new Dictionary<string, double>(Comparer)
                {
                    [BoostActionTypes.Dismantle] = 4
                },
                [ResourceTypes.GhodiumHydride] = new Dictionary<string, double>(Comparer)
                {
                    [BoostActionTypes.UpgradeController] = 1.5
                },
                [ResourceTypes.GhodiumAcid] = new Dictionary<string, double>(Comparer)
                {
                    [BoostActionTypes.UpgradeController] = 1.8
                },
                [ResourceTypes.CatalyzedGhodiumAcid] = new Dictionary<string, double>(Comparer)
                {
                    [BoostActionTypes.UpgradeController] = 2
                }
            },
            [BodyPartType.Attack] = new Dictionary<string, IReadOnlyDictionary<string, double>>(Comparer)
            {
                [ResourceTypes.UtriumHydride] = new Dictionary<string, double>(Comparer)
                {
                    [BoostActionTypes.Attack] = 2
                },
                [ResourceTypes.UtriumAcid] = new Dictionary<string, double>(Comparer)
                {
                    [BoostActionTypes.Attack] = 3
                },
                [ResourceTypes.CatalyzedUtriumAcid] = new Dictionary<string, double>(Comparer)
                {
                    [BoostActionTypes.Attack] = 4
                }
            },
            [BodyPartType.RangedAttack] = new Dictionary<string, IReadOnlyDictionary<string, double>>(Comparer)
            {
                [ResourceTypes.KeaniumOxide] = new Dictionary<string, double>(Comparer)
                {
                    [BoostActionTypes.RangedAttack] = 2,
                    [BoostActionTypes.RangedMassAttack] = 2
                },
                [ResourceTypes.KeaniumAlkalide] = new Dictionary<string, double>(Comparer)
                {
                    [BoostActionTypes.RangedAttack] = 3,
                    [BoostActionTypes.RangedMassAttack] = 3
                },
                [ResourceTypes.CatalyzedKeaniumAlkalide] = new Dictionary<string, double>(Comparer)
                {
                    [BoostActionTypes.RangedAttack] = 4,
                    [BoostActionTypes.RangedMassAttack] = 4
                }
            },
            [BodyPartType.Heal] = new Dictionary<string, IReadOnlyDictionary<string, double>>(Comparer)
            {
                [ResourceTypes.LemergiumOxide] = new Dictionary<string, double>(Comparer)
                {
                    [BoostActionTypes.Heal] = 2,
                    [BoostActionTypes.RangedHeal] = 2
                },
                [ResourceTypes.LemergiumAlkalide] = new Dictionary<string, double>(Comparer)
                {
                    [BoostActionTypes.Heal] = 3,
                    [BoostActionTypes.RangedHeal] = 3
                },
                [ResourceTypes.CatalyzedLemergiumAlkalide] = new Dictionary<string, double>(Comparer)
                {
                    [BoostActionTypes.Heal] = 4,
                    [BoostActionTypes.RangedHeal] = 4
                }
            },
            [BodyPartType.Carry] = new Dictionary<string, IReadOnlyDictionary<string, double>>(Comparer)
            {
                [ResourceTypes.KeaniumHydride] = new Dictionary<string, double>(Comparer)
                {
                    [BoostActionTypes.Capacity] = 2
                },
                [ResourceTypes.KeaniumAcid] = new Dictionary<string, double>(Comparer)
                {
                    [BoostActionTypes.Capacity] = 3
                },
                [ResourceTypes.CatalyzedKeaniumAcid] = new Dictionary<string, double>(Comparer)
                {
                    [BoostActionTypes.Capacity] = 4
                }
            },
            [BodyPartType.Move] = new Dictionary<string, IReadOnlyDictionary<string, double>>(Comparer)
            {
                [ResourceTypes.ZynthiumOxide] = new Dictionary<string, double>(Comparer)
                {
                    [BoostActionTypes.Fatigue] = 2
                },
                [ResourceTypes.ZynthiumAlkalide] = new Dictionary<string, double>(Comparer)
                {
                    [BoostActionTypes.Fatigue] = 3
                },
                [ResourceTypes.CatalyzedZynthiumAlkalide] = new Dictionary<string, double>(Comparer)
                {
                    [BoostActionTypes.Fatigue] = 4
                }
            },
            [BodyPartType.Tough] = new Dictionary<string, IReadOnlyDictionary<string, double>>(Comparer)
            {
                [ResourceTypes.GhodiumOxide] = new Dictionary<string, double>(Comparer)
                {
                    [BoostActionTypes.Damage] = 0.7
                },
                [ResourceTypes.GhodiumAlkalide] = new Dictionary<string, double>(Comparer)
                {
                    [BoostActionTypes.Damage] = 0.5
                },
                [ResourceTypes.CatalyzedGhodiumAlkalide] = new Dictionary<string, double>(Comparer)
                {
                    [BoostActionTypes.Damage] = 0.3
                }
            }
        };

    /// <summary>
    /// Checks if a given mineral type can boost the specified body part type.
    /// </summary>
    /// <param name="partType">The body part type to check.</param>
    /// <param name="mineralType">The mineral type to check.</param>
    /// <returns>True if the mineral can boost this body part type, false otherwise.</returns>
    public static bool CanBoost(BodyPartType partType, string mineralType)
    {
        if (!Multipliers.TryGetValue(partType, out var mineralMap))
            return false;

        var result = mineralMap.ContainsKey(mineralType);
        return result;
    }

    /// <summary>
    /// Attempts to retrieve the boost multiplier for a specific action.
    /// </summary>
    /// <param name="partType">The body part type.</param>
    /// <param name="mineralType">The mineral type boosting the part.</param>
    /// <param name="actionType">The action type (e.g., BoostActionTypes.Harvest, BoostActionTypes.Attack, BoostActionTypes.Capacity).</param>
    /// <param name="multiplier">The boost multiplier if found.</param>
    /// <returns>True if the boost exists for the given combination, false otherwise.</returns>
    public static bool TryGetMultiplier(BodyPartType partType, string mineralType, string actionType, out double multiplier)
    {
        multiplier = 0;

        if (!Multipliers.TryGetValue(partType, out var mineralMap))
            return false;

        if (!mineralMap.TryGetValue(mineralType, out var actionMap))
            return false;

        if (!actionMap.TryGetValue(actionType, out var result))
            return false;

        multiplier = result;
        return true;
    }
}

