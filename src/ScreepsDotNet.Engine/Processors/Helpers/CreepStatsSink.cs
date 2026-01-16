namespace ScreepsDotNet.Engine.Processors.Helpers;

using System.Threading;
using System.Threading.Tasks;
using ScreepsDotNet.Common.Constants;
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
    Task FlushAsync(int gameTime, CancellationToken token = default);
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
    public Task FlushAsync(int gameTime, CancellationToken token = default) => Task.CompletedTask;
}

internal sealed class RoomStatsSink(IRoomStatsUpdater statsUpdater) : ICreepStatsSink
{

    public void IncrementEnergyCreeps(string userId, int amount)
        => Increment(userId, RoomStatsMetricNames.EnergyCreeps, amount);

    public void IncrementCreepsLost(string userId, int bodyParts)
        => Increment(userId, RoomStatsMetricNames.CreepsLost, bodyParts);

    public void IncrementCreepsProduced(string userId, int bodyParts)
        => Increment(userId, RoomStatsMetricNames.CreepsProduced, bodyParts);

    public void IncrementSpawnRenewals(string userId)
        => Increment(userId, RoomStatsMetricNames.SpawnsRenew, 1);

    public void IncrementSpawnRecycles(string userId)
        => Increment(userId, RoomStatsMetricNames.SpawnsRecycle, 1);

    public void IncrementSpawnCreates(string userId)
        => Increment(userId, RoomStatsMetricNames.SpawnsCreate, 1);

    public void IncrementTombstonesCreated(string userId)
        => Increment(userId, RoomStatsMetricNames.TombstonesCreated, 1);

    public void IncrementEnergyConstruction(string userId, int amount)
        => Increment(userId, RoomStatsMetricNames.EnergyConstruction, amount);

    public void IncrementEnergyHarvested(string userId, int amount)
        => Increment(userId, RoomStatsMetricNames.EnergyHarvested, amount);

    public Task FlushAsync(int gameTime, CancellationToken token = default)
        => statsUpdater.FlushAsync(gameTime, token);

    private void Increment(string? userId, string metric, int amount)
    {
        if (string.IsNullOrWhiteSpace(userId) || amount == 0)
            return;

        statsUpdater.Increment(userId, metric, amount);
    }
}
