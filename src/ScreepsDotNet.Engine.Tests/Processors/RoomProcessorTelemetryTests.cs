using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Engine.Telemetry;
using ScreepsDotNet.Engine.Validation.Models;

namespace ScreepsDotNet.Engine.Tests.Processors;

/// <summary>
/// Tests for telemetry emission in RoomProcessor.
/// Note: Full integration tests are deferred - these tests verify telemetry payload structure only.
/// RoomProcessor telemetry emission is tested via existing E2/E3 integration tests.
/// </summary>
public sealed class RoomProcessorTelemetryTests
{
    private const string TestRoomName = "W1N1";
    private const int TestGameTime = 1000;
    private const long TestProcessingTimeMs = 15;
    private const int TestObjectCount = 42;
    private const int TestIntentCount = 10;
    private const int TestValidatedIntentCount = 8;
    private const int TestRejectedIntentCount = 2;
    private const int TestMutationCount = 5;

    private const string IntentValidationStepName = "IntentValidationStep";
    private const string MovementIntentStepName = "MovementIntentStep";
    private const string HarvestIntentStepName = "HarvestIntentStep";

    private const long IntentValidationStepTimingMs = 5;
    private const long MovementIntentStepTimingMs = 10;
    private const long HarvestIntentStepTimingMs = 8;
    private const long TotalStepTimingMs = 23;

    [Fact]
    public void EngineTelemetryPayload_WithValidData_CreatesCorrectly()
    {
        // Arrange & Act
        var payload = new EngineTelemetryPayload(
            RoomName: TestRoomName,
            GameTime: TestGameTime,
            ProcessingTimeMs: TestProcessingTimeMs,
            ObjectCount: TestObjectCount,
            IntentCount: TestIntentCount,
            ValidatedIntentCount: TestValidatedIntentCount,
            RejectedIntentCount: TestRejectedIntentCount,
            MutationCount: TestMutationCount
        );

        // Assert
        Assert.Equal(TestRoomName, payload.RoomName);
        Assert.Equal(TestGameTime, payload.GameTime);
        Assert.Equal(TestProcessingTimeMs, payload.ProcessingTimeMs);
        Assert.Equal(TestObjectCount, payload.ObjectCount);
        Assert.Equal(TestIntentCount, payload.IntentCount);
        Assert.Equal(TestValidatedIntentCount, payload.ValidatedIntentCount);
        Assert.Equal(TestRejectedIntentCount, payload.RejectedIntentCount);
        Assert.Equal(TestMutationCount, payload.MutationCount);
    }

    [Fact]
    public void EngineTelemetryPayload_WithOptionalData_IncludesRejectionDetails()
    {
        // Arrange & Act
        var rejectionsByErrorCode = new Dictionary<string, int> { [nameof(ValidationErrorCode.NotOwned)] = 2 };
        var rejectionsByIntentType = new Dictionary<string, int>
        {
            [IntentKeys.Harvest] = 1,
            [IntentKeys.Transfer] = 1
        };

        var payload = new EngineTelemetryPayload(
            RoomName: TestRoomName,
            GameTime: TestGameTime,
            ProcessingTimeMs: TestProcessingTimeMs,
            ObjectCount: TestObjectCount,
            IntentCount: TestIntentCount,
            ValidatedIntentCount: TestValidatedIntentCount,
            RejectedIntentCount: TestRejectedIntentCount,
            MutationCount: TestMutationCount,
            RejectionsByErrorCode: rejectionsByErrorCode,
            RejectionsByIntentType: rejectionsByIntentType
        );

        // Assert
        Assert.NotNull(payload.RejectionsByErrorCode);
        Assert.Equal(2, payload.RejectionsByErrorCode[nameof(ValidationErrorCode.NotOwned)]);
        Assert.NotNull(payload.RejectionsByIntentType);
        Assert.Equal(1, payload.RejectionsByIntentType[IntentKeys.Transfer]);
        Assert.Equal(1, payload.RejectionsByIntentType[IntentKeys.Transfer]);
    }

    [Fact]
    public void EngineTelemetryPayload_WithoutOptionalData_HasNullOptionalFields()
    {
        // Arrange & Act
        var payload = new EngineTelemetryPayload(
            RoomName: TestRoomName,
            GameTime: TestGameTime,
            ProcessingTimeMs: TestProcessingTimeMs,
            ObjectCount: TestObjectCount,
            IntentCount: TestIntentCount,
            ValidatedIntentCount: 0,
            RejectedIntentCount: 0,
            MutationCount: TestMutationCount
        );

        // Assert
        Assert.Null(payload.RejectionsByErrorCode);
        Assert.Null(payload.RejectionsByIntentType);
        Assert.Null(payload.StepTimingsMs);
    }

    [Fact]
    public void EngineTelemetryPayload_WithStepTimings_IncludesTimingData()
    {
        // Arrange & Act
        var stepTimings = new Dictionary<string, long>
        {
            [IntentValidationStepName] = IntentValidationStepTimingMs,
            [MovementIntentStepName] = MovementIntentStepTimingMs,
            [HarvestIntentStepName] = HarvestIntentStepTimingMs
        };

        var payload = new EngineTelemetryPayload(
            RoomName: TestRoomName,
            GameTime: TestGameTime,
            ProcessingTimeMs: TotalStepTimingMs,
            ObjectCount: TestObjectCount,
            IntentCount: TestIntentCount,
            ValidatedIntentCount: TestIntentCount,
            RejectedIntentCount: 0,
            MutationCount: TestMutationCount,
            StepTimingsMs: stepTimings
        );

        // Assert
        Assert.NotNull(payload.StepTimingsMs);
        Assert.Equal(IntentValidationStepTimingMs, payload.StepTimingsMs[IntentValidationStepName]);
        Assert.Equal(MovementIntentStepTimingMs, payload.StepTimingsMs[MovementIntentStepName]);
        Assert.Equal(HarvestIntentStepTimingMs, payload.StepTimingsMs[HarvestIntentStepName]);
    }
}


