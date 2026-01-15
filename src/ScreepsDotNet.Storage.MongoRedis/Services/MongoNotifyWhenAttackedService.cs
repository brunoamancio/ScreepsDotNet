using ScreepsDotNet.Common.Constants;

namespace ScreepsDotNet.Storage.MongoRedis.Services;

using System;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

public sealed class MongoNotifyWhenAttackedService(IMongoDatabaseProvider databaseProvider) : INotifyWhenAttackedService
{
    private readonly IMongoCollection<RoomObjectDocument> _roomObjects = databaseProvider.GetCollection<RoomObjectDocument>(databaseProvider.Settings.RoomObjectsCollection);

    public async Task<NotifyWhenAttackedResult> SetNotifyWhenAttackedAsync(string structureId, string userId, bool enabled, CancellationToken cancellationToken = default)
    {
        if (!ObjectId.TryParse(structureId, out var objectId))
            return new NotifyWhenAttackedResult(NotifyWhenAttackedResultStatus.StructureNotFound, "structure not found");

        var structure = await _roomObjects.Find(doc => doc.Id == objectId)
                                          .FirstOrDefaultAsync(cancellationToken)
                                          .ConfigureAwait(false);

        if (structure is null)
            return new NotifyWhenAttackedResult(NotifyWhenAttackedResultStatus.StructureNotFound, "structure not found");

        var authorized = await IsUserAuthorizedAsync(structure, userId, cancellationToken).ConfigureAwait(false);
        if (!authorized)
            return new NotifyWhenAttackedResult(NotifyWhenAttackedResultStatus.NotOwner, "not owner");

        var update = Builders<RoomObjectDocument>.Update.Set(doc => doc.NotifyWhenAttacked, enabled);
        await _roomObjects.UpdateOneAsync(doc => doc.Id == objectId, update, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new NotifyWhenAttackedResult(NotifyWhenAttackedResultStatus.Success);
    }

    private async Task<bool> IsUserAuthorizedAsync(RoomObjectDocument structure, string userId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(structure.UserId))
            return string.Equals(structure.UserId, userId, StringComparison.Ordinal);

        if (string.IsNullOrWhiteSpace(structure.Room))
            return false;

        var controllerFilter = Builders<RoomObjectDocument>.Filter.And(
            Builders<RoomObjectDocument>.Filter.Eq(doc => doc.Room, structure.Room),
            Builders<RoomObjectDocument>.Filter.Eq(doc => doc.Type, RoomObjectType.Controller.ToDocumentValue()));

        if (!string.IsNullOrWhiteSpace(structure.Shard))
            controllerFilter &= Builders<RoomObjectDocument>.Filter.Eq(doc => doc.Shard, structure.Shard);

        var controller = await _roomObjects.Find(controllerFilter)
                                           .FirstOrDefaultAsync(cancellationToken)
                                           .ConfigureAwait(false);

        if (controller is null)
            return false;

        if (!string.IsNullOrWhiteSpace(controller.UserId))
            return string.Equals(controller.UserId, userId, StringComparison.Ordinal);

        return string.Equals(controller.Reservation?.UserId, userId, StringComparison.Ordinal);
    }
}
