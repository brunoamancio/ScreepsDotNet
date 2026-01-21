using ScreepsDotNet.Common.Constants;

namespace ScreepsDotNet.Engine.Validation.Constants;

/// <summary>
/// Permission rules for intent validation.
/// Defines which intents require ownership, reservation, or are blocked by safe mode/ramparts.
/// Extracted from Node.js engine intent processors.
/// </summary>
public static class PermissionRules
{
    /// <summary>
    /// Intents that require the actor to own the target controller.
    /// </summary>
    public static readonly IReadOnlySet<string> RequiresControllerOwnership = new HashSet<string>(StringComparer.Ordinal)
    {
        IntentKeys.UpgradeController
    };

    /// <summary>
    /// Intents that require the actor to own OR reserve the target room.
    /// </summary>
    public static readonly IReadOnlySet<string> RequiresOwnedOrReservedRoom = new HashSet<string>(StringComparer.Ordinal)
    {
        IntentKeys.Harvest,
        IntentKeys.ReserveController
    };

    /// <summary>
    /// Intents that are blocked when the target room has active safe mode.
    /// Safe mode prevents hostile creeps from executing attack intents.
    /// </summary>
    public static readonly IReadOnlySet<string> BlockedBySafeMode = new HashSet<string>(StringComparer.Ordinal)
    {
        IntentKeys.Attack,
        IntentKeys.RangedAttack,
        IntentKeys.AttackController
    };

    /// <summary>
    /// Intents that can be blocked by ramparts.
    /// If target is protected by a hostile rampart, the intent is redirected to the rampart.
    /// </summary>
    public static readonly IReadOnlySet<string> BlockedByRampart = new HashSet<string>(StringComparer.Ordinal)
    {
        IntentKeys.Attack,
        IntentKeys.RangedAttack,
        IntentKeys.Repair,
        IntentKeys.Transfer,
        IntentKeys.Withdraw
    };

    /// <summary>
    /// Check if an intent requires controller ownership.
    /// </summary>
    public static bool RequiresOwnership(string intentType)
        => RequiresControllerOwnership.Contains(intentType);

    /// <summary>
    /// Check if an intent requires owned or reserved room.
    /// </summary>
    public static bool RequiresOwnedOrReserved(string intentType)
        => RequiresOwnedOrReservedRoom.Contains(intentType);

    /// <summary>
    /// Check if an intent is blocked by safe mode.
    /// </summary>
    public static bool IsBlockedBySafeMode(string intentType)
        => BlockedBySafeMode.Contains(intentType);

    /// <summary>
    /// Check if an intent can be blocked by ramparts.
    /// </summary>
    public static bool CanBeBlockedByRampart(string intentType)
        => BlockedByRampart.Contains(intentType);
}
