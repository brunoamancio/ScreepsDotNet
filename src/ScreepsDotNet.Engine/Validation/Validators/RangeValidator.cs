using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Validation.Constants;
using ScreepsDotNet.Engine.Validation.Models;

namespace ScreepsDotNet.Engine.Validation.Validators;

/// <summary>
/// Validates Chebyshev distance constraints for all intent types.
/// Range 1 intents: attack, harvest, build, repair, transfer, withdraw, etc.
/// Range 3 intents: rangedAttack, rangedHeal, upgradeController, reserveController, attackController.
/// Unknown intent types default to range 1 (adjacent).
/// </summary>
public sealed class RangeValidator : IIntentValidator
{
    /// <summary>
    /// Validate intent range constraint using Chebyshev distance.
    /// </summary>
    public ValidationResult Validate(IntentRecord intent, RoomSnapshot roomSnapshot)
    {
        // Extract coordinates from intent arguments
        if (!TryGetCoordinates(intent, roomSnapshot, out var actorX, out var actorY, out var targetX, out var targetY)) {
            // If coordinates are missing, assume validation passed
            // (SchemaValidator will catch missing fields)
            return ValidationResult.Success;
        }

        // Get required range for this intent type (defaults to 1 if unknown)
        var requiredRange = ValidationRanges.GetRange(intent.Name);

        // Calculate Chebyshev distance
        var distance = ValidationRanges.ChebyshevDistance(actorX, actorY, targetX, targetY);

        // Validate distance is within required range
        if (distance > requiredRange) {
            var result = ValidationResult.Failure(ValidationErrorCode.NotInRange);
            return result;
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Try to extract actor and target coordinates from intent arguments and room state.
    /// For now, we look for explicit coordinates in arguments. In future, we'll look up
    /// object positions from roomSnapshot when only IDs are provided.
    /// </summary>
    private static bool TryGetCoordinates(
        IntentRecord intent,
        RoomSnapshot roomSnapshot,
        out int actorX,
        out int actorY,
        out int targetX,
        out int targetY)
    {
        actorX = 0;
        actorY = 0;
        targetX = 0;
        targetY = 0;

        // For MVP, we expect coordinates in first argument's fields
        if (intent.Arguments.Count == 0)
            return false;

        var fields = intent.Arguments[0].Fields;

        // Try to get all four coordinates
        var hasActorX = fields.TryGetValue("actorX", out var actorXField) && actorXField.NumberValue.HasValue;
        var hasActorY = fields.TryGetValue("actorY", out var actorYField) && actorYField.NumberValue.HasValue;
        var hasTargetX = fields.TryGetValue("targetX", out var targetXField) && targetXField.NumberValue.HasValue;
        var hasTargetY = fields.TryGetValue("targetY", out var targetYField) && targetYField.NumberValue.HasValue;

        if (hasActorX && hasActorY && hasTargetX && hasTargetY) {
            actorX = actorXField!.NumberValue!.Value;
            actorY = actorYField!.NumberValue!.Value;
            targetX = targetXField!.NumberValue!.Value;
            targetY = targetYField!.NumberValue!.Value;
            return true;
        }

        return false;
    }
}
