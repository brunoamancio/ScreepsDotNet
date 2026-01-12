namespace ScreepsDotNet.Driver.Abstractions.Runtime;

public interface IRuntimeThrottleRegistry
{
    void RegisterThrottle(string userId, TimeSpan duration);
    bool TryGetDelay(string userId, out TimeSpan delay);
    void Clear(string userId);
}
