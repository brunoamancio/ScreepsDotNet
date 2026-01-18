namespace ScreepsDotNet.Engine.Data.GlobalMutations;

using ScreepsDotNet.Driver.Abstractions.GlobalProcessing;
using ScreepsDotNet.Driver.Contracts;

internal sealed class GlobalMutationWriter(IGlobalMutationDispatcher dispatcher) : IGlobalMutationWriter
{
    private readonly List<PowerCreepMutation> _powerCreepMutations = [];
    private readonly List<MarketOrderMutation> _marketOrderMutations = [];
    private readonly List<UserMoneyMutation> _userMoneyMutations = [];
    private readonly List<UserMoneyLogEntry> _userMoneyEntries = [];
    private readonly List<RoomObjectMutation> _roomObjectMutations = [];
    private readonly List<TransactionLogEntry> _transactionEntries = [];
    private readonly List<UserResourceMutation> _userResourceMutations = [];
    private readonly List<UserResourceLogEntry> _userResourceEntries = [];

    public void PatchPowerCreep(string powerCreepId, PowerCreepMutationPatch patch)
    {
        if (string.IsNullOrWhiteSpace(powerCreepId))
            return;

        _powerCreepMutations.Add(new PowerCreepMutation(powerCreepId, PowerCreepMutationType.Patch, Patch: patch));
    }

    public void RemovePowerCreep(string powerCreepId)
    {
        if (string.IsNullOrWhiteSpace(powerCreepId))
            return;

        _powerCreepMutations.Add(new PowerCreepMutation(powerCreepId, PowerCreepMutationType.Remove));
    }

    public void UpsertPowerCreep(PowerCreepSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _powerCreepMutations.Add(new PowerCreepMutation(snapshot.Id, PowerCreepMutationType.Upsert, Snapshot: snapshot));
    }

    public void UpsertMarketOrder(MarketOrderSnapshot snapshot, bool isInterShard)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _marketOrderMutations.Add(new MarketOrderMutation(snapshot.Id, MarketOrderMutationType.Upsert, isInterShard, Snapshot: snapshot));
    }

    public void PatchMarketOrder(string orderId, MarketOrderPatch patch, bool isInterShard)
    {
        if (string.IsNullOrWhiteSpace(orderId) || patch is null)
            return;

        _marketOrderMutations.Add(new MarketOrderMutation(orderId, MarketOrderMutationType.Patch, isInterShard, Patch: patch));
    }

    public void RemoveMarketOrder(string orderId, bool isInterShard)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            return;

        _marketOrderMutations.Add(new MarketOrderMutation(orderId, MarketOrderMutationType.Remove, isInterShard));
    }

    public void AdjustUserMoney(string userId, double newBalance)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return;

        _userMoneyMutations.Add(new UserMoneyMutation(userId, newBalance));
    }

    public void InsertUserMoneyLog(UserMoneyLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _userMoneyEntries.Add(entry);
    }

    public void UpsertRoomObject(RoomObjectSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _roomObjectMutations.Add(new RoomObjectMutation(snapshot.Id, RoomObjectMutationType.Upsert, Snapshot: snapshot));
    }

    public void PatchRoomObject(string objectId, GlobalRoomObjectPatch patch)
    {
        if (string.IsNullOrWhiteSpace(objectId) || patch is null)
            return;

        _roomObjectMutations.Add(new RoomObjectMutation(objectId, RoomObjectMutationType.Patch, Patch: patch));
    }

    public void RemoveRoomObject(string objectId)
    {
        if (string.IsNullOrWhiteSpace(objectId))
            return;

        _roomObjectMutations.Add(new RoomObjectMutation(objectId, RoomObjectMutationType.Remove));
    }

    public void InsertTransaction(TransactionLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _transactionEntries.Add(entry);
    }

    public void AdjustUserResource(string userId, string resourceType, int newBalance)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(resourceType))
            return;

        _userResourceMutations.Add(new UserResourceMutation(userId, resourceType, newBalance));
    }

    public void InsertUserResourceLog(UserResourceLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _userResourceEntries.Add(entry);
    }

    public async Task FlushAsync(CancellationToken token = default)
    {
        if (_powerCreepMutations.Count == 0 &&
            _marketOrderMutations.Count == 0 &&
            _userMoneyMutations.Count == 0 &&
            _userMoneyEntries.Count == 0 &&
            _roomObjectMutations.Count == 0 &&
            _transactionEntries.Count == 0 &&
            _userResourceMutations.Count == 0 &&
            _userResourceEntries.Count == 0) {
            return;
        }

        var batch = new GlobalMutationBatch(
            [.. _powerCreepMutations],
            [.. _marketOrderMutations],
            [.. _userMoneyMutations],
            [.. _userMoneyEntries],
            [.. _roomObjectMutations],
            [.. _transactionEntries],
            [.. _userResourceMutations],
            [.. _userResourceEntries]);
        await dispatcher.ApplyAsync(batch, token).ConfigureAwait(false);
        Reset();
    }

    public void Reset()
    {
        _powerCreepMutations.Clear();
        _marketOrderMutations.Clear();
        _userMoneyMutations.Clear();
        _userMoneyEntries.Clear();
        _roomObjectMutations.Clear();
        _transactionEntries.Clear();
        _userResourceMutations.Clear();
        _userResourceEntries.Clear();
    }
}
