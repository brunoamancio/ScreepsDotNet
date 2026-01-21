using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.Rooms;

namespace ScreepsDotNet.Engine.Validation;

/// <summary>
/// Centralized intent validation pipeline.
/// Coordinates multiple validators in order: Schema → State → Range → Permission → Resource.
/// Stops at first validation failure (early-exit).
/// </summary>
public interface IIntentPipeline
{
    /// <summary>
    /// Validate all intents for a room and return only valid intents.
    /// Invalid intents are silently dropped (Node.js parity).
    /// </summary>
    /// <param name="intents">Intents to validate</param>
    /// <param name="roomState">Current room state snapshot</param>
    /// <returns>List of valid intents that passed all validators</returns>
    Task<IReadOnlyList<IntentRecord>> ValidateAsync(IReadOnlyList<IntentRecord> intents, IRoomStateProvider roomState);
}
