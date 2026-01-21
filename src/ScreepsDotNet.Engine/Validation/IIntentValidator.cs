using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Validation.Models;

namespace ScreepsDotNet.Engine.Validation;

/// <summary>
/// Interface for intent validators.
/// Validators check if an intent can be executed based on:
/// - Schema (payload structure)
/// - State (object existence, spawning, hits, etc.)
/// - Range (Chebyshev distance)
/// - Permission (ownership, safe mode, ramparts)
/// - Resources (availability, capacity)
///
/// For Node.js parity, validation failures are silent (no error messages to user).
/// Validators are synchronous for performance - they receive a RoomSnapshot directly.
/// </summary>
public interface IIntentValidator
{
    /// <summary>
    /// Validate an intent against the current room state snapshot.
    /// </summary>
    /// <param name="intent">The intent to validate</param>
    /// <param name="roomSnapshot">Current room state snapshot (synchronous)</param>
    /// <returns>ValidationResult indicating success or failure with error code</returns>
    ValidationResult Validate(IntentRecord intent, RoomSnapshot roomSnapshot);
}
