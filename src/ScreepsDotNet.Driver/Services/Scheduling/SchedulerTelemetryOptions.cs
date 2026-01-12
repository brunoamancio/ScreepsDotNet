namespace ScreepsDotNet.Driver.Services.Scheduling;

public sealed class SchedulerTelemetryOptions
{
    public int FailureThreshold { get; set; } = 5;
    public TimeSpan ThrottleDuration { get; set; } = TimeSpan.FromSeconds(5);
}
