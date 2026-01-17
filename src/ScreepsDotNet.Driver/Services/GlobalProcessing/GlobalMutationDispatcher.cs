namespace ScreepsDotNet.Driver.Services.GlobalProcessing;

using System.Collections.Generic;
using MongoDB.Bson;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Abstractions.Bulk;
using ScreepsDotNet.Driver.Abstractions.GlobalProcessing;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

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

        var userMoneyWriter = bulkWriterFactory.CreateUsersMoneyWriter();
        var hasMoneyEntryOps = ApplyUserMoneyLogEntries(userMoneyWriter, batch.UserMoneyLogEntries);

        if (hasPowerOps)
            await powerWriter.ExecuteAsync(token).ConfigureAwait(false);
        if (hasMarketOps) {
            if (marketWriter.HasPendingOperations)
                await marketWriter.ExecuteAsync(token).ConfigureAwait(false);
            if (interShardWriter.HasPendingOperations)
                await interShardWriter.ExecuteAsync(token).ConfigureAwait(false);
        }
        if (hasUserMoneyOps)
            await usersWriter.ExecuteAsync(token).ConfigureAwait(false);
        if (hasMoneyEntryOps)
            await userMoneyWriter.ExecuteAsync(token).ConfigureAwait(false);
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

    private static string NormalizeId(string id)
        => ObjectId.TryParse(id, out var objectId) ? objectId.ToString() : id;
}
