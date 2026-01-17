namespace ScreepsDotNet.Engine.Processors.Steps;

using System;
using System.Collections.Generic;
using System.Linq;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Processors.Helpers;

internal sealed class HarvestIntentStep(IResourceDropHelper resourceDropHelper) : IRoomProcessorStep
{
    private static readonly StringComparer Comparer = StringComparer.Ordinal;

    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        var intents = context.State.Intents;
        if (intents?.Users is null || intents.Users.Count == 0)
            return Task.CompletedTask;

        var storeLedger = new Dictionary<string, Dictionary<string, int>>(Comparer);
        var dropContext = resourceDropHelper.CreateContext();
        var roomController = ResolveRoomController(context.State.Objects);

        foreach (var envelope in intents.Users.Values) {
            if (envelope?.ObjectIntents is null || envelope.ObjectIntents.Count == 0)
                continue;

            foreach (var (objectId, records) in envelope.ObjectIntents) {
                if (string.IsNullOrWhiteSpace(objectId) || records.Count == 0)
                    continue;

                if (!context.State.Objects.TryGetValue(objectId, out var creep))
                    continue;

                if (!creep.IsCreep(includePowerCreep: false))
                    continue;

                if (creep.IsSpawning == true)
                    continue;

                if (!string.Equals(creep.UserId, envelope.UserId, StringComparison.Ordinal))
                    continue;

                foreach (var record in records) {
                    if (!string.Equals(record.Name, IntentKeys.Harvest, StringComparison.Ordinal))
                        continue;

                    ProcessHarvest(context, creep, record, storeLedger, dropContext, roomController);
                }
            }
        }

        return Task.CompletedTask;
    }

    private void ProcessHarvest(
        RoomProcessorContext context,
        RoomObjectSnapshot creep,
        IntentRecord record,
        Dictionary<string, Dictionary<string, int>> storeLedger,
        ResourceDropContext dropContext,
        RoomObjectSnapshot? roomController)
    {
        if (!TryGetTargetId(record, out var targetId))
            return;

        if (!context.State.Objects.TryGetValue(targetId, out var target))
            return;

        if (!IsAdjacent(creep, target))
            return;

        switch (target.Type) {
            case RoomObjectTypes.Source:
                HandleSourceHarvest(context, creep, target, storeLedger, dropContext, roomController);
                break;
            case RoomObjectTypes.Mineral:
                HandleMineralHarvest(context, creep, target, storeLedger, dropContext, roomController);
                break;
            case RoomObjectTypes.Deposit:
                HandleDepositHarvest(context, creep, target, storeLedger, dropContext);
                break;
            default:
                break;
        }
    }

    private void HandleSourceHarvest(
        RoomProcessorContext context,
        RoomObjectSnapshot creep,
        RoomObjectSnapshot source,
        Dictionary<string, Dictionary<string, int>> storeLedger,
        ResourceDropContext dropContext,
        RoomObjectSnapshot? roomController)
    {
        if (source.Energy is null or <= 0)
            return;

        if (!CanHarvestSource(roomController, creep.UserId))
            return;

        if (!WorkPartHelper.TryGetActiveWorkParts(creep, out var workParts))
            return;

        var basePower = workParts.Count * ScreepsGameConstants.HarvestPower;
        var cappedPower = Math.Min(source.Energy.Value, basePower);
        if (cappedPower <= 0)
            return;

        var boosted = WorkPartHelper.ApplyWorkBoosts(workParts, cappedPower, WorkBoostEffect.Harvest, ScreepsGameConstants.HarvestPower);
        var amount = (int)Math.Min(source.Energy.Value, boosted);
        if (amount <= 0)
            return;

        var remainingEnergy = Math.Max(source.Energy.Value - amount, 0);
        var invaderHarvested = (source.InvaderHarvested ?? 0) + amount;

        context.MutationWriter.Patch(source.Id, new RoomObjectPatchPayload
        {
            Energy = remainingEnergy,
            InvaderHarvested = invaderHarvested
        });

        var store = GetMutableStore(creep, storeLedger);
        var newAmount = store.TryGetValue(ResourceTypes.Energy, out var current)
            ? current + amount
            : amount;

        store[ResourceTypes.Energy] = newAmount;

        context.MutationWriter.Patch(creep.Id, new RoomObjectPatchPayload
        {
            Store = new Dictionary<string, int>(1, Comparer)
            {
                [ResourceTypes.Energy] = newAmount
            },
            ActionLog = new RoomObjectActionLogPatch(
                Harvest: new RoomObjectActionLogHarvest(source.X, source.Y))
        });

        HandleOverflowIfNeeded(context, creep, store, dropContext);

        if (!string.IsNullOrWhiteSpace(creep.UserId))
            context.Stats.IncrementEnergyHarvested(creep.UserId!, amount);
    }

    private void HandleMineralHarvest(
        RoomProcessorContext context,
        RoomObjectSnapshot creep,
        RoomObjectSnapshot mineral,
        Dictionary<string, Dictionary<string, int>> storeLedger,
        ResourceDropContext dropContext,
        RoomObjectSnapshot? roomController)
    {
        if (mineral.MineralAmount is null or <= 0)
            return;

        if (string.IsNullOrWhiteSpace(mineral.MineralType))
            return;

        var extractor = FindExtractor(context.State.Objects, mineral);
        if (extractor is null)
            return;

        if (!string.IsNullOrWhiteSpace(extractor.UserId) &&
            !string.Equals(extractor.UserId, creep.UserId, StringComparison.Ordinal)) {
            return;
        }

        if (!IsStructureControllerAligned(extractor, roomController))
            return;

        if (extractor.Cooldown is > 0)
            return;

        if (!WorkPartHelper.TryGetActiveWorkParts(creep, out var workParts))
            return;

        var basePower = workParts.Count * ScreepsGameConstants.HarvestMineralPower;
        var cappedPower = Math.Min(mineral.MineralAmount.Value, basePower);
        if (cappedPower <= 0)
            return;

        var boosted = WorkPartHelper.ApplyWorkBoosts(workParts, cappedPower, WorkBoostEffect.Harvest, ScreepsGameConstants.HarvestMineralPower);
        var amount = (int)Math.Min(mineral.MineralAmount.Value, boosted);
        if (amount <= 0)
            return;

        var remainingMineral = mineral.MineralAmount.Value - amount;
        context.MutationWriter.Patch(mineral.Id, new RoomObjectPatchPayload
        {
            MineralAmount = remainingMineral
        });

        var store = GetMutableStore(creep, storeLedger);
        var newAmount = store.TryGetValue(mineral.MineralType, out var current)
            ? current + amount
            : amount;
        store[mineral.MineralType] = newAmount;

        context.MutationWriter.Patch(creep.Id, new RoomObjectPatchPayload
        {
            Store = new Dictionary<string, int>(1, Comparer)
            {
                [mineral.MineralType] = newAmount
            },
            ActionLog = new RoomObjectActionLogPatch(
                Harvest: new RoomObjectActionLogHarvest(mineral.X, mineral.Y))
        });

        HandleOverflowIfNeeded(context, creep, store, dropContext);

        context.MutationWriter.Patch(extractor.Id, new RoomObjectPatchPayload
        {
            Cooldown = ScreepsGameConstants.ExtractorCooldown
        });
    }

    private void HandleDepositHarvest(
        RoomProcessorContext context,
        RoomObjectSnapshot creep,
        RoomObjectSnapshot deposit,
        Dictionary<string, Dictionary<string, int>> storeLedger,
        ResourceDropContext dropContext)
    {
        if (string.IsNullOrWhiteSpace(deposit.DepositType))
            return;

        if (deposit.CooldownTime.HasValue && deposit.CooldownTime.Value > context.State.GameTime)
            return;

        if (!WorkPartHelper.TryGetActiveWorkParts(creep, out var workParts))
            return;

        var amount = workParts.Count * ScreepsGameConstants.HarvestDepositPower;
        if (amount <= 0)
            return;

        var store = GetMutableStore(creep, storeLedger);
        var newAmount = store.TryGetValue(deposit.DepositType, out var current)
            ? current + amount
            : amount;
        store[deposit.DepositType] = newAmount;

        context.MutationWriter.Patch(creep.Id, new RoomObjectPatchPayload
        {
            Store = new Dictionary<string, int>(1, Comparer)
            {
                [deposit.DepositType] = newAmount
            },
            ActionLog = new RoomObjectActionLogPatch(
                Harvest: new RoomObjectActionLogHarvest(deposit.X, deposit.Y))
        });

        HandleOverflowIfNeeded(context, creep, store, dropContext);

        var harvested = (deposit.Harvested ?? 0) + amount;
        var cooldown = Math.Max(1, (int)Math.Ceiling(ScreepsGameConstants.DepositExhaustMultiply * Math.Pow(harvested, ScreepsGameConstants.DepositExhaustPow)));
        var newCooldownTime = context.State.GameTime + cooldown;
        var decayTime = context.State.GameTime + ScreepsGameConstants.DepositDecayTime;

        var cooldownValue = cooldown > 1 ? cooldown : 0;

        context.MutationWriter.Patch(deposit.Id, new RoomObjectPatchPayload
        {
            Harvested = harvested,
            Cooldown = cooldownValue,
            CooldownTime = newCooldownTime,
            DecayTime = decayTime
        });
    }

    private static bool TryGetTargetId(IntentRecord record, out string targetId)
    {
        targetId = string.Empty;
        if (record.Arguments.Count == 0)
            return false;

        var fields = record.Arguments[0].Fields;
        if (!fields.TryGetValue(IntentKeys.TargetId, out var value))
            return false;

        targetId = value.Kind switch
        {
            IntentFieldValueKind.Text => value.TextValue ?? string.Empty,
            IntentFieldValueKind.Number => value.NumberValue?.ToString() ?? string.Empty,
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(targetId);
    }

    private static bool IsAdjacent(RoomObjectSnapshot a, RoomObjectSnapshot b)
        => string.Equals(a.RoomName, b.RoomName, StringComparison.Ordinal) &&
           Math.Abs(a.X - b.X) <= 1 &&
           Math.Abs(a.Y - b.Y) <= 1;

    private static Dictionary<string, int> GetMutableStore(
        RoomObjectSnapshot creep,
        Dictionary<string, Dictionary<string, int>> storeLedger)
    {
        if (storeLedger.TryGetValue(creep.Id, out var existing))
            return existing;

        var clone = creep.Store.Count == 0
            ? new Dictionary<string, int>(0, Comparer)
            : new Dictionary<string, int>(creep.Store, Comparer);

        storeLedger[creep.Id] = clone;
        return clone;
    }

    private void HandleOverflowIfNeeded(
        RoomProcessorContext context,
        RoomObjectSnapshot creep,
        Dictionary<string, int> store,
        ResourceDropContext dropContext)
    {
        var capacity = creep.StoreCapacity ?? 0;
        if (capacity <= 0)
            return;

        var total = store.Values.Sum();
        if (total <= capacity)
            return;

        var overflow = total - capacity;
        if (overflow <= 0)
            return;

        var storePatch = new Dictionary<string, int>(Comparer);
        resourceDropHelper.DropOverflowResources(context, creep, store, overflow, storePatch, dropContext);
        if (storePatch.Count > 0) {
            context.MutationWriter.Patch(creep.Id, new RoomObjectPatchPayload
            {
                Store = storePatch
            });
        }
    }

    private static RoomObjectSnapshot? ResolveRoomController(IReadOnlyDictionary<string, RoomObjectSnapshot> objects)
    {
        foreach (var obj in objects.Values) {
            if (string.Equals(obj.Type, RoomObjectTypes.Controller, StringComparison.Ordinal))
                return obj;
        }

        return null;
    }

    private static bool CanHarvestSource(RoomObjectSnapshot? controller, string? creepUserId)
    {
        if (controller is null)
            return true;

        if (!string.IsNullOrWhiteSpace(controller.UserId) &&
            !string.Equals(controller.UserId, creepUserId, StringComparison.Ordinal)) {
            return false;
        }

        var reservationUser = controller.Reservation?.UserId;
        return string.IsNullOrWhiteSpace(reservationUser) ||
            string.Equals(reservationUser, creepUserId, StringComparison.Ordinal);
    }

    private static bool IsStructureControllerAligned(RoomObjectSnapshot structure, RoomObjectSnapshot? controller)
        => string.IsNullOrWhiteSpace(structure.UserId) || (controller is not null && controller.Level is not null and not <= 0 && string.Equals(controller.UserId, structure.UserId, StringComparison.Ordinal));

    private static RoomObjectSnapshot? FindExtractor(
        IReadOnlyDictionary<string, RoomObjectSnapshot> objects,
        RoomObjectSnapshot mineral)
    {
        foreach (var obj in objects.Values) {
            if (!string.Equals(obj.RoomName, mineral.RoomName, StringComparison.Ordinal))
                continue;

            if (!string.Equals(obj.Shard, mineral.Shard, StringComparison.Ordinal))
                continue;

            if (obj.X != mineral.X || obj.Y != mineral.Y)
                continue;

            if (!string.Equals(obj.Type, RoomObjectTypes.Extractor, StringComparison.Ordinal) &&
                !string.Equals(obj.StructureType, RoomObjectTypes.Extractor, StringComparison.Ordinal)) {
                continue;
            }

            return obj;
        }

        return null;
    }
}
