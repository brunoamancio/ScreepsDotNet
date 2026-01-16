namespace ScreepsDotNet.Engine.Processors.Helpers;

using System.Threading;
using System.Threading.Tasks;
using ScreepsDotNet.Driver.Abstractions.History;

/// <summary>
/// Placeholder sink for creep-related telemetry. Once stats plumbing exists this can forward to the real collector.
/// </summary>
public interface ICreepStatsSink
{
    void IncrementEnergyCreeps(string userId, int amount);
    void IncrementCreepsLost(string userId, int bodyParts);
    void IncrementCreepsProduced(string userId, int bodyParts);
    void IncrementSpawnRenewals(string userId);
    void IncrementSpawnRecycles(string userId);
    void IncrementSpawnCreates(string userId);
    void IncrementTombstonesCreated(string userId);
    void IncrementEnergyConstruction(string userId, int amount);
    void IncrementEnergyHarvested(string userId, int amount);
    Task FlushAsync(CancellationToken token = default);
}

internal sealed class NullCreepStatsSink : ICreepStatsSink
{
    public void IncrementEnergyCreeps(string userId, int amount) { }
    public void IncrementCreepsLost(string userId, int bodyParts) { }
    public void IncrementCreepsProduced(string userId, int bodyParts) { }
    public void IncrementSpawnRenewals(string userId) { }
    public void IncrementSpawnRecycles(string userId) { }
    public void IncrementSpawnCreates(string userId) { }
    public void IncrementTombstonesCreated(string userId) { }
    public void IncrementEnergyConstruction(string userId, int amount) { }
    public void IncrementEnergyHarvested(string userId, int amount) { }
    public Task FlushAsync(CancellationToken token = default) => Task.CompletedTask;
}

internal sealed class RoomStatsSink(IRoomStatsUpdater statsUpdater) : ICreepStatsSink
{
    private const string EnergyCreepsMetric = "energyCreeps";
    private const string CreepsLostMetric = "creepsLost";
    private const string CreepsProducedMetric = "creepsProduced";
    private const string SpawnRenewMetric = "spawnsRenew";
    private const string SpawnRecycleMetric = "spawnsRecycle";
    private const string SpawnCreateMetric = "spawnsCreate";
    private const string TombstonesMetric = "tombstonesCreated";
    private const string EnergyConstructionMetric = "energyConstruction";
    private const string EnergyHarvestedMetric = "energyHarvested";

    public void IncrementEnergyCreeps(string userId, int amount)
        => Increment(userId, EnergyCreepsMetric, amount);

    public void IncrementCreepsLost(string userId, int bodyParts)
        => Increment(userId, CreepsLostMetric, bodyParts);

    public void IncrementCreepsProduced(string userId, int bodyParts)
        => Increment(userId, CreepsProducedMetric, bodyParts);

    public void IncrementSpawnRenewals(string userId)
        => Increment(userId, SpawnRenewMetric, 1);

    public void IncrementSpawnRecycles(string userId)
        => Increment(userId, SpawnRecycleMetric, 1);

    public void IncrementSpawnCreates(string userId)
        => Increment(userId, SpawnCreateMetric, 1);

    public void IncrementTombstonesCreated(string userId)
        => Increment(userId, TombstonesMetric, 1);

    public void IncrementEnergyConstruction(string userId, int amount)
        => Increment(userId, EnergyConstructionMetric, amount);

    public void IncrementEnergyHarvested(string userId, int amount)
        => Increment(userId, EnergyHarvestedMetric, amount);

    public Task FlushAsync(CancellationToken token = default)
        => statsUpdater.FlushAsync(token);

    private void Increment(string? userId, string metric, int amount)
    {
        if (string.IsNullOrWhiteSpace(userId) || amount == 0)
            return;

        statsUpdater.Increment(userId, metric, amount);
    }
}
