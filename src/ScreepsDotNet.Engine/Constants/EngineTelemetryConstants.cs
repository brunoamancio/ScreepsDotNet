namespace ScreepsDotNet.Engine.Constants;

/// <summary>
/// Constants for Engine telemetry and observability.
/// </summary>
public static class EngineTelemetryConstants
{
    /// <summary>
    /// Stage prefix for Engine room processing telemetry when bridged to Driver telemetry.
    /// Format: "engine:room:{roomName}"
    /// </summary>
    public const string EngineRoomStagePrefix = "engine:room:";

    /// <summary>
    /// Formats a stage name for Engine room processing telemetry.
    /// </summary>
    public static string FormatEngineRoomStage(string roomName)
        => $"{EngineRoomStagePrefix}{roomName}";
}
