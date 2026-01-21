using ScreepsDotNet.Driver.Contracts;

namespace ScreepsDotNet.Engine.Validation;

/// <summary>
/// Default implementation of the intent validation pipeline.
/// Orchestrates all registered validators and returns only valid intents.
/// Validators run in order: Schema → State → Range → Permission → Resource.
/// Early-exit: stops at first validation failure (Node.js parity - silent failures).
/// </summary>
internal sealed class IntentValidationPipeline(IEnumerable<IIntentValidator> validators) : IIntentPipeline
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
            var isValid = ValidateIntent(intent, roomSnapshot);
            if (isValid)
                validIntents.Add(intent);
        }

        return validIntents;
    }

    private bool ValidateIntent(IntentRecord intent, RoomSnapshot roomSnapshot)
    {
        foreach (var validator in _validators) {
            var result = validator.Validate(intent, roomSnapshot);
            if (!result.IsValid)
                return false; // Early exit on first failure
        }

        return true; // Passed all validators
    }
}
