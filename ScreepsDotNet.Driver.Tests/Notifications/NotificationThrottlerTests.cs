using ScreepsDotNet.Driver.Services.Notifications;

namespace ScreepsDotNet.Driver.Tests.Notifications;

public sealed class NotificationThrottlerTests
{
    [Fact]
    public void CalculateBucketTimestamp_RoundsUpToInterval()
    {
        var now = new DateTimeOffset(2026, 1, 11, 12, 0, 0, TimeSpan.Zero);
        var later = now.AddMinutes(5);

        var bucket1 = NotificationThrottler.CalculateBucketTimestamp(now, 10);
        var bucket2 = NotificationThrottler.CalculateBucketTimestamp(later, 10);

        Assert.True(bucket2 >= bucket1);
        Assert.Equal(now.ToUnixTimeMilliseconds(), bucket1);
        Assert.Equal(now.AddMinutes(10).ToUnixTimeMilliseconds(), bucket2);
    }

    [Fact]
    public void CalculateBucketTimestamp_ClampsInterval()
    {
        var now = new DateTimeOffset(2026, 1, 11, 12, 0, 0, TimeSpan.Zero);
        var bucketZero = NotificationThrottler.CalculateBucketTimestamp(now, 0);
        var bucketLarge = NotificationThrottler.CalculateBucketTimestamp(now, 10_000);

        Assert.Equal(now.ToUnixTimeMilliseconds(), bucketZero);
        Assert.True(bucketLarge >= now.ToUnixTimeMilliseconds());
    }

    [Fact]
    public void NormalizeMessage_TrimsAndLimits()
    {
        var source = new string('x', 600);
        var normalized = NotificationThrottler.NormalizeMessage($"  {source}  ");

        Assert.Equal(500, normalized.Length);
        Assert.True(normalized.Trim().Length == normalized.Length);
    }
}
