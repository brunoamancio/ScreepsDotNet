using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Validation.Constants;
using ScreepsDotNet.Engine.Validation.Models;

namespace ScreepsDotNet.Engine.Validation.Validators;

/// <summary>
/// Validates actor and target object states.
/// Checks: spawning status, alive/dead, existence, hits property, store property.
/// Extracted from Node.js engine inline validation patterns.
/// </summary>
public sealed class StateValidator : IIntentValidator
{
    /// <summary>
    /// Validate intent state requirements.
    /// </summary>
    public ValidationResult Validate(IntentRecord intent, RoomSnapshot roomSnapshot)
    {
        // Extract actor and target IDs from intent arguments
        if (!TryGetActorAndTargetIds(intent, out var actorId, out var targetId)) {
            // No IDs found - let SchemaValidator handle this
            return ValidationResult.Success;
        }

        // Validate actor state
        if (!roomSnapshot.Objects.TryGetValue(actorId, out var actor)) {
            var actorNotFoundResult = ValidationResult.Failure(ValidationErrorCode.ActorNotFound);
            return actorNotFoundResult;
        }

        // Check if actor is dead (hits <= 0)
        if (actor.Hits.HasValue && actor.Hits.Value <= 0) {
            var actorDeadResult = ValidationResult.Failure(ValidationErrorCode.ActorDead);
            return actorDeadResult;
        }

        // Check if actor cannot be spawning for this intent
        if (StateRequirements.ActorMustNotBeSpawning(intent.Name) && actor.IsSpawning == true) {
            var actorSpawningResult = ValidationResult.Failure(ValidationErrorCode.ActorSpawning);
            return actorSpawningResult;
        }

        // Check if actor must have store
        if (StateRequirements.RequiresActorStore(intent.Name) && actor.Store is null) {
            var actorNoStoreResult = ValidationResult.Failure(ValidationErrorCode.ActorNoStore);
            return actorNoStoreResult;
        }

        // Validate target state (if targetId is provided)
        if (!string.IsNullOrEmpty(targetId)) {
            if (!roomSnapshot.Objects.TryGetValue(targetId, out var target)) {
                var targetNotFoundResult = ValidationResult.Failure(ValidationErrorCode.TargetNotFound);
                return targetNotFoundResult;
            }

            // Check if target is self
            if (targetId == actorId) {
                var targetIsSelfResult = ValidationResult.Failure(ValidationErrorCode.TargetSameAsActor);
                return targetIsSelfResult;
            }

            // Check if target cannot be spawning
            if (StateRequirements.TargetMustNotBeSpawning(intent.Name) && target.IsSpawning == true) {
                var targetSpawningResult = ValidationResult.Failure(ValidationErrorCode.TargetSpawning);
                return targetSpawningResult;
            }

            // Check if target must have hits
            if (StateRequirements.RequiresTargetHits(intent.Name) && !target.Hits.HasValue) {
                var targetNoHitsResult = ValidationResult.Failure(ValidationErrorCode.TargetNoHits);
                return targetNoHitsResult;
            }

            // Check if target must have store
            if (StateRequirements.RequiresTargetStore(intent.Name) && target.Store is null) {
                var targetNoStoreResult = ValidationResult.Failure(ValidationErrorCode.TargetNoStore);
                return targetNoStoreResult;
            }
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Try to extract actor and target IDs from intent arguments.
    /// </summary>
    private static bool TryGetActorAndTargetIds(IntentRecord intent, out string actorId, out string? targetId)
    {
        actorId = string.Empty;
        targetId = null;

        // Check if intent has arguments
        if (intent.Arguments.Count == 0)
            return false;

        var fields = intent.Arguments[0].Fields;

        // Try to get actor ID
        if (!fields.TryGetValue(IntentKeys.ActorId, out var actorIdField) || actorIdField.TextValue is null)
            return false;

        actorId = actorIdField.TextValue;

        // Try to get target ID (optional)
        if (fields.TryGetValue("targetId", out var targetIdField) && targetIdField.TextValue is not null) {
            targetId = targetIdField.TextValue;
        }

        return true;
    }
}
