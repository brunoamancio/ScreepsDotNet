using ScreepsDotNet.Common.Constants;

namespace ScreepsDotNet.Engine.Validation.Constants;

/// <summary>
/// Range requirements for each intent type (Chebyshev distance).
/// Extracted from Node.js engine intent processors.
/// </summary>
public static class ValidationRanges
{
    private static readonly IReadOnlyDictionary<string, int> IntentRanges = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        // Range 1 (Adjacent) - Most common
        [IntentKeys.Attack] = 1,
        [IntentKeys.Harvest] = 1,
        [IntentKeys.Build] = 1,
        [IntentKeys.Repair] = 1,
        [IntentKeys.Transfer] = 1,
        [IntentKeys.Withdraw] = 1,
        [IntentKeys.Pickup] = 1,
        [IntentKeys.Drop] = 1,
        [IntentKeys.Heal] = 1,
        [IntentKeys.BoostCreep] = 1,
        [IntentKeys.UnboostCreep] = 1,

        // Range 3
        [IntentKeys.RangedAttack] = 3,
        [IntentKeys.RangedHeal] = 3,
        [IntentKeys.UpgradeController] = 3,
        [IntentKeys.ReserveController] = 3,
        [IntentKeys.AttackController] = 3

        // Special Cases (handled differently in processors)
        // Tower range validated in TowerIntentStep (falloff calculation)
        // Spawn range = 0 (adjacent to spawn structure)
        // RangedMassAttack range = 3 (same as rangedAttack)
    };

    /// <summary>
    /// Get the required range for an intent type.
    /// Returns 1 (adjacent) if intent type not found.
    /// </summary>
    public static int GetRange(string intentType)
        => IntentRanges.GetValueOrDefault(intentType, 1);

    /// <summary>
    /// Calculate Chebyshev distance between two positions.
    /// Formula: max(abs(x1 - x2), abs(y1 - y2))
    /// </summary>
    public static int ChebyshevDistance(int x1, int y1, int x2, int y2)
        => Math.Max(Math.Abs(x1 - x2), Math.Abs(y1 - y2));

    /// <summary>
    /// Check if actor is within range of target.
    /// </summary>
    public static bool IsInRange(int actorX, int actorY, int targetX, int targetY, int requiredRange)
    {
        var distance = ChebyshevDistance(actorX, actorY, targetX, targetY);
        var result = distance <= requiredRange;
        return result;
    }
}
