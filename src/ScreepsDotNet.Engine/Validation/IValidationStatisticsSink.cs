using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Validation.Models;

namespace ScreepsDotNet.Engine.Validation;

/// <summary>
/// Collects and aggregates intent validation statistics for observability.
/// Thread-safe for concurrent validation operations across multiple rooms.
/// </summary>
public interface IValidationStatisticsSink
{
    /// <summary>
    /// Records a validation result for an intent.
    /// Updates aggregated statistics for valid/rejected counts and error distribution.
    /// </summary>
    /// <param name="intent">The validated intent</param>
    /// <param name="result">The validation result (success or failure with error code)</param>
    void RecordValidation(IntentRecord intent, ValidationResult result);

    /// <summary>
    /// Gets a snapshot of current validation statistics.
    /// Returns a copy, not a live reference (safe for diagnostics/telemetry export).
    /// </summary>
    /// <returns>Aggregated validation statistics</returns>
    ValidationStatistics GetStatistics();

    /// <summary>
    /// Resets all statistics counters to zero.
    /// Useful for periodic metric collection (e.g., per-tick or per-batch resets).
    /// </summary>
    void Reset();
}
