namespace ScreepsDotNet.Driver.Services.Notifications;

internal static class NotificationThrottler
{
    private const int MaxIntervalMinutes = 1440;
    private const int MessageLimit = 500;

    public static long CalculateBucketTimestamp(DateTimeOffset now, int groupIntervalMinutes)
    {
        var clamped = Math.Clamp(groupIntervalMinutes, 0, MaxIntervalMinutes);
        var intervalMilliseconds = clamped * 60_000L;
        if (intervalMilliseconds <= 0)
            return now.ToUnixTimeMilliseconds();

        var current = now.ToUnixTimeMilliseconds();
        var buckets = Math.Ceiling(current / (double)intervalMilliseconds);
        return (long)(buckets * intervalMilliseconds);
    }

    public static string NormalizeMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;

        var trimmed = message.Trim();
        return trimmed.Length <= MessageLimit ? trimmed : trimmed[..MessageLimit];
    }
}
