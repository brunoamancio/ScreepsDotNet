namespace ScreepsDotNet.Engine.Processors.Helpers;

/// <summary>
/// Placeholder sink for creep-related telemetry. Once stats plumbing exists this can forward to the real collector.
/// </summary>
internal interface ICreepStatsSink
{
    void IncrementEnergyCreeps(string userId, int amount);
    void IncrementCreepsLost(string userId, int bodyParts);
    void IncrementCreepsProduced(string userId, int bodyParts);
}

internal sealed class NullCreepStatsSink : ICreepStatsSink
{
    public void IncrementEnergyCreeps(string userId, int amount) { }
    public void IncrementCreepsLost(string userId, int bodyParts) { }
    public void IncrementCreepsProduced(string userId, int bodyParts) { }
}
