using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Validation.Models;

namespace ScreepsDotNet.Engine.Validation;

/// <summary>
/// Default implementation of the intent validation pipeline.
/// Orchestrates all registered validators and returns only valid intents.
/// Validators run in order: Schema → State → Range → Permission → Resource.
/// Early-exit: stops at first validation failure (Node.js parity - silent failures).
/// Records validation statistics for observability and diagnostics (E3.4).
/// </summary>
internal sealed class IntentValidationPipeline(IEnumerable<IIntentValidator> validators, IValidationStatisticsSink statisticsSink) : IIntentPipeline
{
    private readonly IReadOnlyList<IIntentValidator> _validators = validators.ToList();

    public IReadOnlyList<IntentRecord> Validate(IReadOnlyList<IntentRecord> intents, RoomSnapshot roomSnapshot)
    {
        if (intents.Count == 0)
            return intents;

        if (roomSnapshot.Intents is null)
            return [];

        var validIntents = new List<IntentRecord>(intents.Count);

        foreach (var intent in intents) {
            var validationResult = ValidateIntent(intent, roomSnapshot);

            // Record statistics for observability (E3.4)
            statisticsSink.RecordValidation(intent, validationResult);

            if (validationResult.IsValid)
                validIntents.Add(intent);
        }

        return validIntents;
    }

    private ValidationResult ValidateIntent(IntentRecord intent, RoomSnapshot roomSnapshot)
    {
        foreach (var validator in _validators) {
            var result = validator.Validate(intent, roomSnapshot);
            if (!result.IsValid)
                return result; // Early exit on first failure, return error code
        }

        return ValidationResult.Success; // Passed all validators
    }
}
