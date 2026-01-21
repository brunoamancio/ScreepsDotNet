using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Validation.Models;

namespace ScreepsDotNet.Engine.Validation;

/// <summary>
/// Thread-safe implementation of IValidationStatisticsSink.
/// Tracks aggregated validation metrics for observability and diagnostics.
/// </summary>
internal sealed class ValidationStatisticsSink : IValidationStatisticsSink
{
    private readonly Lock _lock = new();
    private int _totalIntentsValidated;
    private int _validIntentsCount;
    private int _rejectedIntentsCount;
    private readonly Dictionary<ValidationErrorCode, int> _rejectionsByErrorCode = [];
    private readonly Dictionary<string, int> _rejectionsByIntentType = new(StringComparer.Ordinal);

    public void RecordValidation(IntentRecord intent, ValidationResult result)
    {
        lock (_lock) {
            _totalIntentsValidated++;

            if (result.IsValid) {
                _validIntentsCount++;
            } else {
                _rejectedIntentsCount++;

                // Track error code distribution
                if (result.ErrorCode.HasValue) {
                    var currentCount = _rejectionsByErrorCode.GetValueOrDefault(result.ErrorCode.Value, 0);
                    _rejectionsByErrorCode[result.ErrorCode.Value] = currentCount + 1;
                }

                // Track intent type distribution
                var intentName = intent.Name;
                var intentNameCount = _rejectionsByIntentType.GetValueOrDefault(intentName, 0);
                _rejectionsByIntentType[intentName] = intentNameCount + 1;
            }
        }
    }

    public ValidationStatistics GetStatistics()
    {
        lock (_lock) {
            var statistics = new ValidationStatistics
            {
                TotalIntentsValidated = _totalIntentsValidated,
                ValidIntentsCount = _validIntentsCount,
                RejectedIntentsCount = _rejectedIntentsCount,
                RejectionsByErrorCode = new Dictionary<ValidationErrorCode, int>(_rejectionsByErrorCode),
                RejectionsByIntentType = new Dictionary<string, int>(_rejectionsByIntentType, StringComparer.Ordinal)
            };

            return statistics;
        }
    }

    public void Reset()
    {
        lock (_lock) {
            _totalIntentsValidated = 0;
            _validIntentsCount = 0;
            _rejectedIntentsCount = 0;
            _rejectionsByErrorCode.Clear();
            _rejectionsByIntentType.Clear();
        }
    }
}
