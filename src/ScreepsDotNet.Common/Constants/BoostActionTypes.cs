namespace ScreepsDotNet.Common.Constants;

/// <summary>
/// Action type keys used in boost multiplier lookups.
/// These correspond to the third level in the BOOSTS table: BOOSTS[bodyPartType][mineralType][actionType].
/// Ported from Node.js BOOSTS constant structure.
/// </summary>
public static class BoostActionTypes
{
    // Work part actions
    public const string Harvest = "harvest";
    public const string Build = "build";
    public const string Repair = "repair";
    public const string Dismantle = "dismantle";
    public const string UpgradeController = "upgradeController";

    // Attack part actions
    public const string Attack = "attack";

    // RangedAttack part actions
    public const string RangedAttack = "rangedAttack";
    public const string RangedMassAttack = "rangedMassAttack";

    // Heal part actions
    public const string Heal = "heal";
    public const string RangedHeal = "rangedHeal";

    // Carry part actions
    public const string Capacity = "capacity";

    // Move part actions
    public const string Fatigue = "fatigue";

    // Tough part actions
    public const string Damage = "damage";
}
