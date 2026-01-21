using ScreepsDotNet.Driver.Contracts;

namespace ScreepsDotNet.Engine.Validation;

/// <summary>
/// Centralized intent validation pipeline.
/// Coordinates multiple validators in order: Schema → State → Range → Permission → Resource.
/// Stops at first validation failure (early-exit).
/// Validators are synchronous for performance.
/// </summary>
public interface IIntentPipeline
{
    /// <summary>
    /// Validate all intents for a room and return only valid intents.
    /// Invalid intents are silently dropped (Node.js parity).
    /// </summary>
    /// <param name="intents">Intents to validate</param>
    /// <param name="roomSnapshot">Current room state snapshot (synchronous)</param>
    /// <returns>List of valid intents that passed all validators</returns>
    IReadOnlyList<IntentRecord> Validate(IReadOnlyList<IntentRecord> intents, RoomSnapshot roomSnapshot);
}
