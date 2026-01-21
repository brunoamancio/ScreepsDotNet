namespace ScreepsDotNet.Engine.Telemetry;

/// <summary>
/// Telemetry payload for Engine room processing metrics.
/// Emitted after each room tick to track processing performance and statistics.
/// </summary>
public sealed record EngineTelemetryPayload(
    string RoomName,
    int GameTime,
    long ProcessingTimeMs,
    int ObjectCount,
    int IntentCount,
    int ValidatedIntentCount,
    int RejectedIntentCount,
    int MutationCount,
    Dictionary<string, int>? RejectionsByErrorCode = null,
    Dictionary<string, int>? RejectionsByIntentType = null,
    Dictionary<string, long>? StepTimingsMs = null);
