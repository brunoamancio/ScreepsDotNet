using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Abstractions;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Engine.Constants;
using ScreepsDotNet.Engine.Telemetry;
using ScreepsDotNet.Engine.Validation.Models;

namespace ScreepsDotNet.Engine.Tests.Telemetry;

public sealed class EngineTelemetrySinkTests
{
    private const string TestRoomName = "W1N1";
    private const string AlternateRoomName = "W5S7";
    private const int TestGameTime = 1000;
    private const int AlternateGameTime = 2500;
    private const long TestProcessingTimeMs = 15;
    private const long AlternateProcessingTimeMs = 20;
    private const int TestObjectCount = 42;
    private const int AlternateObjectCount = 100;
    private const int TestIntentCount = 10;
    private const int AlternateIntentCount = 50;
    private const int TestValidatedIntentCount = 8;
    private const int AlternateValidatedIntentCount = 45;
    private const int TestRejectedIntentCount = 2;
    private const int AlternateRejectedIntentCount = 5;
    private const int TestMutationCount = 5;
    private const int AlternateMutationCount = 30;

    [Fact]
    public async Task PublishRoomTelemetryAsync_ValidPayload_BridgesToDriverHooks()
    {
        // Arrange
        var driverHooks = new FakeDriverLoopHooks();
        var sink = new EngineTelemetrySink(driverHooks);

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

        // Act
        await sink.PublishRoomTelemetryAsync(payload, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(driverHooks.TelemetryPayloads);
        var published = driverHooks.TelemetryPayloads[0];
        Assert.Equal(DriverProcessType.Processor, published.Loop);
        Assert.Equal(TestGameTime, published.GameTime);
        Assert.Equal(EngineTelemetryConstants.FormatEngineRoomStage(TestRoomName), published.Stage);
    }

    [Fact]
    public async Task PublishRoomTelemetryAsync_WithValidationStats_IncludesRejectionData()
    {
        // Arrange
        var driverHooks = new FakeDriverLoopHooks();
        var sink = new EngineTelemetrySink(driverHooks);

        var payload = new EngineTelemetryPayload(
            RoomName: TestRoomName,
            GameTime: TestGameTime,
            ProcessingTimeMs: TestProcessingTimeMs,
            ObjectCount: TestObjectCount,
            IntentCount: TestIntentCount,
            ValidatedIntentCount: TestValidatedIntentCount,
            RejectedIntentCount: TestRejectedIntentCount,
            MutationCount: TestMutationCount,
            RejectionsByErrorCode: new Dictionary<string, int> { [nameof(ValidationErrorCode.NotOwned)] = 2 },
            RejectionsByIntentType: new Dictionary<string, int> { [IntentKeys.Harvest] = 1, [IntentKeys.Transfer] = 1 }
        );

        // Act
        await sink.PublishRoomTelemetryAsync(payload, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(driverHooks.TelemetryPayloads);
        Assert.Equal(EngineTelemetryConstants.FormatEngineRoomStage(TestRoomName), driverHooks.TelemetryPayloads[0].Stage);
    }

    [Fact]
    public async Task PublishRoomTelemetryAsync_WithoutValidationStats_OmitsRejectionData()
    {
        // Arrange
        var driverHooks = new FakeDriverLoopHooks();
        var sink = new EngineTelemetrySink(driverHooks);

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

        // Act
        await sink.PublishRoomTelemetryAsync(payload, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(driverHooks.TelemetryPayloads);
    }

    [Fact]
    public async Task PublishRoomTelemetryAsync_StageFormat_MatchesExpectedPattern()
    {
        // Arrange
        var driverHooks = new FakeDriverLoopHooks();
        var sink = new EngineTelemetrySink(driverHooks);

        var payload = new EngineTelemetryPayload(
            RoomName: AlternateRoomName,
            GameTime: AlternateGameTime,
            ProcessingTimeMs: AlternateProcessingTimeMs,
            ObjectCount: AlternateObjectCount,
            IntentCount: AlternateIntentCount,
            ValidatedIntentCount: AlternateValidatedIntentCount,
            RejectedIntentCount: AlternateRejectedIntentCount,
            MutationCount: AlternateMutationCount
        );

        // Act
        await sink.PublishRoomTelemetryAsync(payload, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(driverHooks.TelemetryPayloads);
        Assert.Equal(EngineTelemetryConstants.FormatEngineRoomStage(AlternateRoomName), driverHooks.TelemetryPayloads[0].Stage);
    }

    [Fact]
    public async Task PublishRoomTelemetryAsync_CancellationToken_PropagatesCorrectly()
    {
        // Arrange
        var driverHooks = new FakeDriverLoopHooks();
        var sink = new EngineTelemetrySink(driverHooks);

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

        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        // Act
        await sink.PublishRoomTelemetryAsync(payload, token);

        // Assert
        Assert.Single(driverHooks.TelemetryPayloads);
    }

    private sealed class FakeDriverLoopHooks : IDriverLoopHooks
    {
        public List<RuntimeTelemetryPayload> TelemetryPayloads { get; } = [];

        public Task SaveRoomHistoryAsync(string roomName, int gameTime, Driver.Contracts.RoomHistoryTickPayload payload, CancellationToken token = default)
            => Task.CompletedTask;

        public Task UploadRoomHistoryChunkAsync(string roomName, int baseGameTime, CancellationToken token = default)
            => Task.CompletedTask;

        public Task PublishConsoleMessagesAsync(string userId, Driver.Abstractions.Notifications.ConsoleMessagesPayload payload, CancellationToken token = default)
            => Task.CompletedTask;

        public Task PublishConsoleErrorAsync(string userId, string errorMessage, CancellationToken token = default)
            => Task.CompletedTask;

        public Task SendNotificationAsync(string userId, string message, Driver.Abstractions.Notifications.NotificationOptions options, CancellationToken token = default)
            => Task.CompletedTask;

        public Task NotifyRoomsDoneAsync(int gameTime, CancellationToken token = default)
            => Task.CompletedTask;

        public Task PublishRuntimeTelemetryAsync(RuntimeTelemetryPayload payload, CancellationToken token = default)
        {
            TelemetryPayloads.Add(payload);
            return Task.CompletedTask;
        }
    }
}

