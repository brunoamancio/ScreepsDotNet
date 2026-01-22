namespace ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

using ScreepsDotNet.Engine.Processors.Helpers;

/// <summary>
/// Stats sink that captures all stats for parity testing
/// </summary>
public sealed class CapturingStatsSink : ICreepStatsSink
{
    public Dictionary<string, int> EnergyHarvested { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, int> EnergyControl { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, int> EnergyCreeps { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, int> EnergyConstruction { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, int> CreepsLost { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, int> CreepsProduced { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, int> SpawnRenewals { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, int> SpawnRecycles { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, int> SpawnCreates { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, int> TombstonesCreated { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, int> PowerProcessed { get; } = new(StringComparer.Ordinal);

    public void IncrementEnergyHarvested(string userId, int amount)
        => Increment(EnergyHarvested, userId, amount);

    public void IncrementEnergyControl(string userId, int amount)
        => Increment(EnergyControl, userId, amount);

    public void IncrementEnergyCreeps(string userId, int amount)
        => Increment(EnergyCreeps, userId, amount);

    public void IncrementEnergyConstruction(string userId, int amount)
        => Increment(EnergyConstruction, userId, amount);

    public void IncrementCreepsLost(string userId, int bodyParts)
        => Increment(CreepsLost, userId, bodyParts);

    public void IncrementCreepsProduced(string userId, int bodyParts)
        => Increment(CreepsProduced, userId, bodyParts);

    public void IncrementSpawnRenewals(string userId)
        => Increment(SpawnRenewals, userId, 1);

    public void IncrementSpawnRecycles(string userId)
        => Increment(SpawnRecycles, userId, 1);

    public void IncrementSpawnCreates(string userId)
        => Increment(SpawnCreates, userId, 1);

    public void IncrementTombstonesCreated(string userId)
        => Increment(TombstonesCreated, userId, 1);

    public void IncrementPowerProcessed(string userId, int amount)
        => Increment(PowerProcessed, userId, amount);

    public Task FlushAsync(int gameTime, CancellationToken token = default)
        => Task.CompletedTask;

    private static void Increment(Dictionary<string, int> dict, string key, int amount)
    {
        if (!dict.TryGetValue(key, out var current))
        {
            current = 0;
        }
        dict[key] = current + amount;
    }

    public void Reset()
    {
        EnergyHarvested.Clear();
        EnergyControl.Clear();
        EnergyCreeps.Clear();
        EnergyConstruction.Clear();
        CreepsLost.Clear();
        CreepsProduced.Clear();
        SpawnRenewals.Clear();
        SpawnRecycles.Clear();
        SpawnCreates.Clear();
        TombstonesCreated.Clear();
        PowerProcessed.Clear();
    }
}
