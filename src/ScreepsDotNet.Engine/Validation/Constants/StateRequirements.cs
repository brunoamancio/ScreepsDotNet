using ScreepsDotNet.Common.Constants;

namespace ScreepsDotNet.Engine.Validation.Constants;

/// <summary>
/// State requirements for intent validation.
/// Defines which object states (spawning, alive, has store, etc.) are required for each intent.
/// Extracted from Node.js engine intent processors.
/// </summary>
public static class StateRequirements
{
    /// <summary>
    /// Intents that require the actor to NOT be spawning.
    /// Most creep actions are blocked while spawning.
    /// </summary>
    public static readonly IReadOnlySet<string> ActorCannotBeSpawning = new HashSet<string>(StringComparer.Ordinal)
    {
        IntentKeys.Attack,
        IntentKeys.RangedAttack,
        IntentKeys.Heal,
        IntentKeys.RangedHeal,
        IntentKeys.Harvest,
        IntentKeys.Build,
        IntentKeys.Repair,
        IntentKeys.UpgradeController,
        IntentKeys.ReserveController,
        IntentKeys.AttackController,
        IntentKeys.Transfer,
        IntentKeys.Withdraw,
        IntentKeys.Pickup,
        IntentKeys.Drop,
        IntentKeys.Move,
        IntentKeys.Pull,
        IntentKeys.Say,
        IntentKeys.Suicide
    };

    /// <summary>
    /// Intents that require the target to NOT be spawning.
    /// </summary>
    public static readonly IReadOnlySet<string> TargetCannotBeSpawning = new HashSet<string>(StringComparer.Ordinal)
    {
        IntentKeys.Attack,
        IntentKeys.RangedAttack,
        IntentKeys.Heal,
        IntentKeys.RangedHeal,
        IntentKeys.Transfer
    };

    /// <summary>
    /// Intents that require the target to have hits (be damageable).
    /// </summary>
    public static readonly IReadOnlySet<string> TargetMustHaveHits = new HashSet<string>(StringComparer.Ordinal)
    {
        IntentKeys.Attack,
        IntentKeys.RangedAttack,
        IntentKeys.Repair
    };

    /// <summary>
    /// Intents that require the actor to have a store.
    /// </summary>
    public static readonly IReadOnlySet<string> ActorMustHaveStore = new HashSet<string>(StringComparer.Ordinal)
    {
        IntentKeys.Transfer,
        IntentKeys.Withdraw,
        IntentKeys.Pickup,
        IntentKeys.Drop,
        IntentKeys.Build,
        IntentKeys.Repair,
        IntentKeys.UpgradeController
    };

    /// <summary>
    /// Intents that require the target to have a store.
    /// </summary>
    public static readonly IReadOnlySet<string> TargetMustHaveStore = new HashSet<string>(StringComparer.Ordinal)
    {
        IntentKeys.Transfer,
        IntentKeys.Withdraw
    };

    /// <summary>
    /// Check if actor cannot be spawning for this intent.
    /// </summary>
    public static bool ActorMustNotBeSpawning(string intentType)
        => ActorCannotBeSpawning.Contains(intentType);

    /// <summary>
    /// Check if target cannot be spawning for this intent.
    /// </summary>
    public static bool TargetMustNotBeSpawning(string intentType)
        => TargetCannotBeSpawning.Contains(intentType);

    /// <summary>
    /// Check if target must have hits for this intent.
    /// </summary>
    public static bool RequiresTargetHits(string intentType)
        => TargetMustHaveHits.Contains(intentType);

    /// <summary>
    /// Check if actor must have store for this intent.
    /// </summary>
    public static bool RequiresActorStore(string intentType)
        => ActorMustHaveStore.Contains(intentType);

    /// <summary>
    /// Check if target must have store for this intent.
    /// </summary>
    public static bool RequiresTargetStore(string intentType)
        => TargetMustHaveStore.Contains(intentType);
}
