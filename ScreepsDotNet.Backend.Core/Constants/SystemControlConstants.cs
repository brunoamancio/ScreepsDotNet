namespace ScreepsDotNet.Backend.Core.Constants;

/// <summary>
/// Defines the Redis keys and channels used to control the Screeps runtime via the CLI.
/// </summary>
public static class SystemControlConstants
{
    /// <summary>
    /// Redis string key indicating whether the main loop is paused ("1" when paused, "0" otherwise).
    /// </summary>
    public const string MainLoopPausedKey = "runtime:mainLoopPaused";

    /// <summary>
    /// Redis string key storing the minimal tick duration (in milliseconds).
    /// </summary>
    public const string MainLoopMinimumDurationKey = "runtime:mainLoopMinDuration";

    /// <summary>
    /// Redis pub/sub channel used to broadcast server-wide chat/system messages.
    /// </summary>
    public const string ServerMessageChannel = "serverMessage";

    /// <summary>
    /// Redis pub/sub channel notified when the tick duration changes so workers can react.
    /// </summary>
    public const string TickRateChannel = "setTickRate";

    /// <summary>
    /// Default minimal tick duration applied when resetting storage (milliseconds).
    /// </summary>
    public const int DefaultTickDurationMilliseconds = 1000;
}
