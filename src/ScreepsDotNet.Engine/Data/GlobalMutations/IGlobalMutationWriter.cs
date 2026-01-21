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
    void UpsertRoomObject(RoomObjectSnapshot snapshot);
    void PatchRoomObject(string objectId, GlobalRoomObjectPatch patch);
    void RemoveRoomObject(string objectId);
    void InsertTransaction(TransactionLogEntry entry);
    void AdjustUserResource(string userId, string resourceType, int newBalance);
    void InsertUserResourceLog(UserResourceLogEntry entry);
    void IncrementUserGcl(string userId, int amount);
    void IncrementUserPower(string userId, double amount);
    void DecrementUserPower(string userId, double amount);
    Task FlushAsync(CancellationToken token = default);
    void Reset();
}
