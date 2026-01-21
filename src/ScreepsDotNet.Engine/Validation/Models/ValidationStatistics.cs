namespace ScreepsDotNet.Engine.Validation.Models;

/// <summary>
/// Aggregated statistics about intent validation operations.
/// Tracks total validated intents, valid/rejected counts, and error distribution.
/// </summary>
public sealed record ValidationStatistics
{
    /// <summary>
    /// Total number of intents validated (valid + rejected).
    /// </summary>
    public int TotalIntentsValidated { get; init; }

    /// <summary>
    /// Number of intents that passed all validators.
    /// </summary>
    public int ValidIntentsCount { get; init; }

    /// <summary>
    /// Number of intents that failed at least one validator.
    /// </summary>
    public int RejectedIntentsCount { get; init; }

    /// <summary>
    /// Distribution of rejections by error code.
    /// Key: ValidationErrorCode, Value: Count of rejections with that error code.
    /// </summary>
    public IReadOnlyDictionary<ValidationErrorCode, int> RejectionsByErrorCode { get; init; } = new Dictionary<ValidationErrorCode, int>();

    /// <summary>
    /// Distribution of rejections by intent type.
    /// Key: Intent type (e.g., "attack", "harvest"), Value: Count of rejections for that intent type.
    /// </summary>
    public IReadOnlyDictionary<string, int> RejectionsByIntentType { get; init; } = new Dictionary<string, int>(StringComparer.Ordinal);
}
