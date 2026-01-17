namespace ScreepsDotNet.Engine.Data.GlobalMutations;

using ScreepsDotNet.Driver.Contracts;

public interface IGlobalMutationWriter
{
    void PatchPowerCreep(string powerCreepId, PowerCreepMutationPatch patch);
    void RemovePowerCreep(string powerCreepId);
    void UpsertPowerCreep(PowerCreepSnapshot snapshot);
    void UpsertMarketOrder(MarketOrderSnapshot snapshot, bool isInterShard);
    void PatchMarketOrder(string orderId, MarketOrderPatch patch, bool isInterShard);
    void RemoveMarketOrder(string orderId, bool isInterShard);
    void AdjustUserMoney(string userId, double newBalance);
    void InsertUserMoneyLog(UserMoneyLogEntry entry);
    Task FlushAsync(CancellationToken token = default);
    void Reset();
}
