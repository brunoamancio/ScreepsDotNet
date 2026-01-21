using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Validation;
using ScreepsDotNet.Engine.Validation.Models;

namespace ScreepsDotNet.Engine.Tests.Validation;

public sealed class ValidationStatisticsSinkTests
{
    [Fact]
    public void RecordValidation_ValidIntent_IncrementsValidCount()
    {
        // Arrange
        var sink = new ValidationStatisticsSink();
        var intent = CreateTestIntent(IntentKeys.Attack);
        var result = ValidationResult.Success;

        // Act
        sink.RecordValidation(intent, result);
        var stats = sink.GetStatistics();

        // Assert
        Assert.Equal(1, stats.TotalIntentsValidated);
        Assert.Equal(1, stats.ValidIntentsCount);
        Assert.Equal(0, stats.RejectedIntentsCount);
    }

    [Fact]
    public void RecordValidation_RejectedIntent_IncrementsRejectedCount()
    {
        // Arrange
        var sink = new ValidationStatisticsSink();
        var intent = CreateTestIntent(IntentKeys.Attack);
        var result = ValidationResult.Failure(ValidationErrorCode.NotInRange);

        // Act
        sink.RecordValidation(intent, result);
        var stats = sink.GetStatistics();

        // Assert
        Assert.Equal(1, stats.TotalIntentsValidated);
        Assert.Equal(0, stats.ValidIntentsCount);
        Assert.Equal(1, stats.RejectedIntentsCount);
    }

    [Fact]
    public void RecordValidation_RejectedIntent_TracksErrorCode()
    {
        // Arrange
        var sink = new ValidationStatisticsSink();
        var intent = CreateTestIntent(IntentKeys.Attack);
        var result = ValidationResult.Failure(ValidationErrorCode.SafeModeActive);

        // Act
        sink.RecordValidation(intent, result);
        var stats = sink.GetStatistics();

        // Assert
        Assert.Single(stats.RejectionsByErrorCode);
        var rejectionCount = stats.RejectionsByErrorCode.GetValueOrDefault(ValidationErrorCode.SafeModeActive, 0);
        Assert.Equal(1, rejectionCount);
    }

    [Fact]
    public void RecordValidation_RejectedIntent_TracksIntentType()
    {
        // Arrange
        var sink = new ValidationStatisticsSink();
        var intent = CreateTestIntent(IntentKeys.Attack);
        var result = ValidationResult.Failure(ValidationErrorCode.NotInRange);

        // Act
        sink.RecordValidation(intent, result);
        var stats = sink.GetStatistics();

        // Assert
        Assert.Single(stats.RejectionsByIntentType);
        var rejectionCount = stats.RejectionsByIntentType.GetValueOrDefault(IntentKeys.Attack, 0);
        Assert.Equal(1, rejectionCount);
    }

    [Fact]
    public void RecordValidation_MultipleIntents_AggregatesCorrectly()
    {
        // Arrange
        var sink = new ValidationStatisticsSink();

        // Act
        sink.RecordValidation(CreateTestIntent(IntentKeys.Attack), ValidationResult.Success);
        sink.RecordValidation(CreateTestIntent(IntentKeys.Harvest), ValidationResult.Success);
        sink.RecordValidation(CreateTestIntent(IntentKeys.Attack), ValidationResult.Failure(ValidationErrorCode.NotInRange));
        sink.RecordValidation(CreateTestIntent(IntentKeys.Attack), ValidationResult.Failure(ValidationErrorCode.SafeModeActive));
        sink.RecordValidation(CreateTestIntent(IntentKeys.Build), ValidationResult.Failure(ValidationErrorCode.InsufficientEnergy));

        var stats = sink.GetStatistics();

        // Assert
        Assert.Equal(5, stats.TotalIntentsValidated);
        Assert.Equal(2, stats.ValidIntentsCount);
        Assert.Equal(3, stats.RejectedIntentsCount);

        // Check error code distribution
        Assert.Equal(3, stats.RejectionsByErrorCode.Count);
        Assert.Equal(1, stats.RejectionsByErrorCode[ValidationErrorCode.NotInRange]);
        Assert.Equal(1, stats.RejectionsByErrorCode[ValidationErrorCode.SafeModeActive]);
        Assert.Equal(1, stats.RejectionsByErrorCode[ValidationErrorCode.InsufficientEnergy]);

        // Check intent type distribution
        Assert.Equal(2, stats.RejectionsByIntentType.Count);
        Assert.Equal(2, stats.RejectionsByIntentType[IntentKeys.Attack]);
        Assert.Equal(1, stats.RejectionsByIntentType[IntentKeys.Build]);
    }

    [Fact]
    public void RecordValidation_SameErrorCodeMultipleTimes_AccumulatesCount()
    {
        // Arrange
        var sink = new ValidationStatisticsSink();

        // Act
        sink.RecordValidation(CreateTestIntent(IntentKeys.Attack), ValidationResult.Failure(ValidationErrorCode.NotInRange));
        sink.RecordValidation(CreateTestIntent(IntentKeys.Harvest), ValidationResult.Failure(ValidationErrorCode.NotInRange));
        sink.RecordValidation(CreateTestIntent(IntentKeys.Build), ValidationResult.Failure(ValidationErrorCode.NotInRange));

        var stats = sink.GetStatistics();

        // Assert
        Assert.Equal(3, stats.RejectedIntentsCount);
        Assert.Single(stats.RejectionsByErrorCode);
        Assert.Equal(3, stats.RejectionsByErrorCode[ValidationErrorCode.NotInRange]);
    }

    [Fact]
    public void Reset_ClearsAllStatistics()
    {
        // Arrange
        var sink = new ValidationStatisticsSink();
        sink.RecordValidation(CreateTestIntent(IntentKeys.Attack), ValidationResult.Success);
        sink.RecordValidation(CreateTestIntent(IntentKeys.Harvest), ValidationResult.Failure(ValidationErrorCode.NotInRange));

        // Act
        sink.Reset();
        var stats = sink.GetStatistics();

        // Assert
        Assert.Equal(0, stats.TotalIntentsValidated);
        Assert.Equal(0, stats.ValidIntentsCount);
        Assert.Equal(0, stats.RejectedIntentsCount);
        Assert.Empty(stats.RejectionsByErrorCode);
        Assert.Empty(stats.RejectionsByIntentType);
    }

    [Fact]
    public void GetStatistics_ReturnsSnapshotNotLiveReference()
    {
        // Arrange
        var sink = new ValidationStatisticsSink();
        sink.RecordValidation(CreateTestIntent(IntentKeys.Attack), ValidationResult.Success);

        // Act
        var stats1 = sink.GetStatistics();
        sink.RecordValidation(CreateTestIntent(IntentKeys.Harvest), ValidationResult.Success);
        var stats2 = sink.GetStatistics();

        // Assert - stats1 should not be affected by subsequent recordings
        Assert.Equal(1, stats1.TotalIntentsValidated);
        Assert.Equal(2, stats2.TotalIntentsValidated);
    }

    [Fact]
    public async Task RecordValidation_ThreadSafety_HandlesConflictingWrites()
    {
        // Arrange
        var sink = new ValidationStatisticsSink();
        var intentCount = 1000;
        var tasks = new List<Task>();

        // Act - Simulate concurrent validation recording
        for (var i = 0; i < intentCount; i++) {
            var taskNumber = i;
            var task = Task.Run(() =>
            {
                var isValid = taskNumber % 2 == 0;
                var validationResult = isValid
                    ? ValidationResult.Success
                    : ValidationResult.Failure(ValidationErrorCode.NotInRange);

                var intent = CreateTestIntent(IntentKeys.Attack);
                sink.RecordValidation(intent, validationResult);
            }, TestContext.Current.CancellationToken);
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        var stats = sink.GetStatistics();

        // Assert - All intents should be recorded
        Assert.Equal(intentCount, stats.TotalIntentsValidated);
        Assert.Equal(intentCount / 2, stats.ValidIntentsCount);
        Assert.Equal(intentCount / 2, stats.RejectedIntentsCount);
    }

    [Fact]
    public void GetStatistics_InitialState_ReturnsEmptyStatistics()
    {
        // Arrange
        var sink = new ValidationStatisticsSink();

        // Act
        var stats = sink.GetStatistics();

        // Assert
        Assert.Equal(0, stats.TotalIntentsValidated);
        Assert.Equal(0, stats.ValidIntentsCount);
        Assert.Equal(0, stats.RejectedIntentsCount);
        Assert.Empty(stats.RejectionsByErrorCode);
        Assert.Empty(stats.RejectionsByIntentType);
    }

    private static IntentRecord CreateTestIntent(string intentName)
    {
        var intent = new IntentRecord(
            Name: intentName,
            Arguments: []);

        return intent;
    }
}
