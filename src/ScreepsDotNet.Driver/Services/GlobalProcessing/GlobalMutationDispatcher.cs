namespace ScreepsDotNet.Driver.Services.GlobalProcessing;

using System.Collections.Generic;
using MongoDB.Bson;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Abstractions.Bulk;
using ScreepsDotNet.Driver.Abstractions.GlobalProcessing;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;
using static ScreepsDotNet.Common.Constants.RoomDocumentFields;
using static ScreepsDotNet.Common.Constants.TransactionLogFields;
using static ScreepsDotNet.Common.Constants.UserResourceLogFields;

internal sealed class GlobalMutationDispatcher(IBulkWriterFactory bulkWriterFactory) : IGlobalMutationDispatcher
{
    public async Task ApplyAsync(GlobalMutationBatch batch, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var powerWriter = bulkWriterFactory.CreateUsersPowerCreepsWriter();
        var hasPowerOps = ApplyPowerCreepMutations(powerWriter, batch.PowerCreepMutations);

        var marketWriter = bulkWriterFactory.CreateMarketOrdersWriter();
        var interShardWriter = bulkWriterFactory.CreateMarketIntershardOrdersWriter();
        var hasMarketOps = ApplyMarketOrderMutations(marketWriter, interShardWriter, batch.MarketOrderMutations);

        var usersWriter = bulkWriterFactory.CreateUsersWriter();
        var hasUserMoneyOps = ApplyUserMoneyMutations(usersWriter, batch.UserMoneyMutations);
        var hasUserResourceOps = ApplyUserResourceMutations(usersWriter, batch.UserResourceMutations);

        var userMoneyWriter = bulkWriterFactory.CreateUsersMoneyWriter();
        var hasMoneyEntryOps = ApplyUserMoneyLogEntries(userMoneyWriter, batch.UserMoneyLogEntries);

        var roomObjectWriter = bulkWriterFactory.CreateRoomObjectsWriter();
        var hasRoomObjectOps = ApplyRoomObjectMutations(roomObjectWriter, batch.RoomObjectMutations);

        var transactionWriter = bulkWriterFactory.CreateTransactionsWriter();
        var hasTransactionOps = ApplyTransactionLogEntries(transactionWriter, batch.TransactionLogEntries);

        var userResourceWriter = bulkWriterFactory.CreateUsersResourcesWriter();
        var hasUserResourceEntryOps = ApplyUserResourceLogEntries(userResourceWriter, batch.UserResourceLogEntries);

        if (hasPowerOps)
            await powerWriter.ExecuteAsync(token).ConfigureAwait(false);
        if (hasMarketOps) {
            if (marketWriter.HasPendingOperations)
                await marketWriter.ExecuteAsync(token).ConfigureAwait(false);
            if (interShardWriter.HasPendingOperations)
                await interShardWriter.ExecuteAsync(token).ConfigureAwait(false);
        }
        if (hasUserMoneyOps || hasUserResourceOps)
            await usersWriter.ExecuteAsync(token).ConfigureAwait(false);
        if (hasMoneyEntryOps)
            await userMoneyWriter.ExecuteAsync(token).ConfigureAwait(false);
        if (hasRoomObjectOps)
            await roomObjectWriter.ExecuteAsync(token).ConfigureAwait(false);
        if (hasTransactionOps)
            await transactionWriter.ExecuteAsync(token).ConfigureAwait(false);
        if (hasUserResourceEntryOps)
            await userResourceWriter.ExecuteAsync(token).ConfigureAwait(false);
    }

    private static bool ApplyPowerCreepMutations(IBulkWriter<PowerCreepDocument> writer, IReadOnlyList<PowerCreepMutation> mutations)
    {
        if (mutations.Count == 0)
            return false;

        foreach (var mutation in mutations) {
            if (string.IsNullOrWhiteSpace(mutation.Id))
                continue;

            switch (mutation.Type) {
                case PowerCreepMutationType.Patch:
                    if (mutation.Patch is null)
                        continue;
                    var patchDocument = BuildPatchDocument(mutation.Patch);
                    if (patchDocument.ElementCount == 0)
                        continue;
                    writer.Update(NormalizeId(mutation.Id), patchDocument);
                    break;

                case PowerCreepMutationType.Remove:
                    writer.Remove(NormalizeId(mutation.Id));
                    break;

                case PowerCreepMutationType.Upsert:
                    if (mutation.Snapshot is null)
                        continue;
                    var document = PowerCreepDocumentMapper.ToDocument(mutation.Snapshot);
                    writer.Insert(document, NormalizeId(mutation.Id));
                    break;
                default:
                    break;
            }
        }

        return writer.HasPendingOperations;
    }

    private static BsonDocument BuildPatchDocument(PowerCreepMutationPatch patch)
    {
        var document = new BsonDocument();
        if (!string.IsNullOrWhiteSpace(patch.Name))
            document[PowerCreepDocumentFields.Name] = patch.Name;
        if (patch.Level.HasValue)
            document[PowerCreepDocumentFields.Level] = patch.Level.Value;
        if (patch.HitsMax.HasValue)
            document[PowerCreepDocumentFields.HitsMax] = patch.HitsMax.Value;
        if (patch.StoreCapacity.HasValue)
            document[PowerCreepDocumentFields.StoreCapacity] = patch.StoreCapacity.Value;
        if (patch.SpawnCooldownTime.HasValue)
            document[PowerCreepDocumentFields.SpawnCooldownTime] = patch.SpawnCooldownTime.Value;
        if (patch.DeleteTime.HasValue)
            document[PowerCreepDocumentFields.DeleteTime] = patch.DeleteTime.Value;
        else if (patch.ClearDeleteTime)
            document[PowerCreepDocumentFields.DeleteTime] = BsonNull.Value;
        if (!string.IsNullOrWhiteSpace(patch.Shard))
            document[PowerCreepDocumentFields.Shard] = patch.Shard;
        if (patch.Powers is not null && patch.Powers.Count > 0) {
            var powers = new BsonDocument();
            foreach (var (powerId, powerSnapshot) in patch.Powers) {
                if (string.IsNullOrWhiteSpace(powerId))
                    continue;
                powers[powerId] = new BsonDocument(PowerCreepDocumentFields.Level, powerSnapshot.Level);
            }
            if (powers.ElementCount > 0)
                document[PowerCreepDocumentFields.Powers] = powers;
        }

        return document;
    }

    private static bool ApplyMarketOrderMutations(
        IBulkWriter<MarketOrderDocument> regularWriter,
        IBulkWriter<MarketOrderDocument> interShardWriter,
        IReadOnlyList<MarketOrderMutation> mutations)
    {
        if (mutations.Count == 0)
            return false;

        foreach (var mutation in mutations) {
            if (string.IsNullOrWhiteSpace(mutation.Id))
                continue;

            var writer = mutation.IsInterShard ? interShardWriter : regularWriter;

            switch (mutation.Type) {
                case MarketOrderMutationType.Upsert:
                    if (mutation.Snapshot is null)
                        continue;
                    var document = MarketOrderDocumentMapper.ToDocument(mutation.Snapshot);
                    writer.Insert(document, NormalizeId(mutation.Snapshot.Id));
                    break;

                case MarketOrderMutationType.Patch:
                    if (mutation.Patch is null)
                        continue;
                    var patchDocument = MarketOrderDocumentMapper.BuildPatchDocument(mutation.Patch);
                    if (patchDocument.ElementCount == 0)
                        continue;
                    writer.Update(NormalizeId(mutation.Id), patchDocument);
                    break;

                case MarketOrderMutationType.Remove:
                    writer.Remove(NormalizeId(mutation.Id));
                    break;
                default:
                    break;
            }
        }

        return regularWriter.HasPendingOperations || interShardWriter.HasPendingOperations;
    }

    private static bool ApplyUserMoneyMutations(IBulkWriter<UserDocument> writer, IReadOnlyList<UserMoneyMutation> mutations)
    {
        if (mutations.Count == 0)
            return false;

        foreach (var mutation in mutations) {
            if (string.IsNullOrWhiteSpace(mutation.UserId))
                continue;

            var update = new BsonDocument(UserDocumentFields.Money, mutation.NewMoney);
            writer.Update(mutation.UserId, update);
        }

        return writer.HasPendingOperations;
    }

    private static bool ApplyUserMoneyLogEntries(IBulkWriter<UserMoneyEntryDocument> writer, IReadOnlyList<UserMoneyLogEntry> entries)
    {
        if (entries.Count == 0)
            return false;

        foreach (var entry in entries) {
            var document = UserMoneyEntryDocumentMapper.ToDocument(entry);
            writer.Insert(document);
        }

        return writer.HasPendingOperations;
    }

    private static bool ApplyRoomObjectMutations(IBulkWriter<RoomObjectDocument> writer, IReadOnlyList<RoomObjectMutation> mutations)
    {
        if (mutations.Count == 0)
            return false;

        foreach (var mutation in mutations) {
            if (string.IsNullOrWhiteSpace(mutation.Id))
                continue;

            switch (mutation.Type) {
                case RoomObjectMutationType.Upsert:
                    if (mutation.Snapshot is null)
                        continue;
                    var document = RoomObjectSnapshotMapper.ToDocument(mutation.Snapshot);
                    writer.Insert(document, NormalizeId(mutation.Id));
                    break;

                case RoomObjectMutationType.Patch:
                    if (mutation.Patch is null)
                        continue;
                    var bsonPatch = BuildRoomObjectPatchDocument(mutation.Patch);
                    if (bsonPatch.ElementCount == 0)
                        continue;
                    writer.Update(NormalizeId(mutation.Id), bsonPatch);
                    break;

                case RoomObjectMutationType.Remove:
                    writer.Remove(NormalizeId(mutation.Id));
                    break;
                default:
                    break;
            }
        }

        return writer.HasPendingOperations;
    }

    private static bool ApplyTransactionLogEntries(IBulkWriter<BsonDocument> writer, IReadOnlyList<TransactionLogEntry> entries)
    {
        if (entries.Count == 0)
            return false;

        foreach (var entry in entries) {
            var document = new BsonDocument
            {
                [Time] = entry.Tick,
                [Sender] = !string.IsNullOrWhiteSpace(entry.SenderId) ? entry.SenderId : BsonNull.Value,
                [Recipient] = !string.IsNullOrWhiteSpace(entry.RecipientId) ? entry.RecipientId : BsonNull.Value,
                [TransactionLogFields.ResourceType] = entry.ResourceType,
                [Amount] = entry.Amount,
                [From] = entry.FromRoom,
                [To] = entry.ToRoom
            };

            if (!string.IsNullOrWhiteSpace(entry.OrderId))
                document[Order] = entry.OrderId;
            if (!string.IsNullOrWhiteSpace(entry.Description))
                document[Description] = entry.Description;

            writer.Insert(document);
        }

        return writer.HasPendingOperations;
    }

    private static bool ApplyUserResourceMutations(IBulkWriter<UserDocument> writer, IReadOnlyList<UserResourceMutation> mutations)
    {
        if (mutations.Count == 0)
            return false;

        foreach (var mutation in mutations) {
            if (string.IsNullOrWhiteSpace(mutation.UserId) || string.IsNullOrWhiteSpace(mutation.ResourceType))
                continue;

            var update = new BsonDocument($"{UserDocumentFields.Resources}.{mutation.ResourceType}", mutation.NewBalance);
            writer.Update(mutation.UserId, update);
        }

        return writer.HasPendingOperations;
    }

    private static bool ApplyUserResourceLogEntries(IBulkWriter<BsonDocument> writer, IReadOnlyList<UserResourceLogEntry> entries)
    {
        if (entries.Count == 0)
            return false;

        foreach (var entry in entries) {
            var document = new BsonDocument
            {
                [Date] = entry.TimestampUtc,
                [UserResourceLogFields.ResourceType] = entry.ResourceType,
                [User] = entry.UserId,
                [Change] = entry.Change,
                [Balance] = entry.Balance
            };

            if (!string.IsNullOrWhiteSpace(entry.MarketOrderId))
                document[MarketOrderId] = entry.MarketOrderId;
            if (entry.Metadata is not null && entry.Metadata.Count > 0)
                document[Market] = BsonDocument.Create(entry.Metadata);

            writer.Insert(document);
        }

        return writer.HasPendingOperations;
    }

    private static BsonDocument BuildRoomObjectPatchDocument(GlobalRoomObjectPatch patch)
    {
        var document = new BsonDocument();
        if (patch.X.HasValue)
            document[RoomObject.X] = patch.X.Value;
        if (patch.Y.HasValue)
            document[RoomObject.Y] = patch.Y.Value;
        if (patch.Hits.HasValue)
            document[RoomObject.Hits] = patch.Hits.Value;
        if (patch.Energy.HasValue)
            document[RoomObject.Energy] = patch.Energy.Value;
        if (patch.EnergyCapacity.HasValue)
            document[RoomObject.EnergyCapacity] = patch.EnergyCapacity.Value;
        if (patch.CooldownTime.HasValue)
            document[RoomObject.CooldownTime] = patch.CooldownTime.Value;
        if (patch.Store is not null && patch.Store.Count > 0)
            document[RoomObject.Store.Root] = new BsonDocument(patch.Store.Select(kvp => new BsonElement(kvp.Key, kvp.Value)));
        if (!string.IsNullOrWhiteSpace(patch.Shard))
            document[RoomObject.Shard] = patch.Shard;
        if (patch.ClearSend)
            document[RoomObject.Send] = BsonNull.Value;
        return document;
    }

    private static string NormalizeId(string id)
        => ObjectId.TryParse(id, out var objectId) ? objectId.ToString() : id;
}
