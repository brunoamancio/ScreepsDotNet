namespace ScreepsDotNet.Driver.Services.GlobalProcessing;

using MongoDB.Bson;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

internal static class MarketOrderDocumentMapper
{
    public static MarketOrderDocument ToDocument(MarketOrderSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new MarketOrderDocument
        {
            Id = ParseObjectId(snapshot.Id),
            Active = snapshot.Active,
            UserId = snapshot.UserId,
            Type = snapshot.Type,
            RoomName = snapshot.RoomName,
            ResourceType = snapshot.ResourceType,
            Price = snapshot.Price,
            Amount = snapshot.Amount,
            RemainingAmount = snapshot.RemainingAmount,
            TotalAmount = snapshot.TotalAmount,
            CreatedTick = snapshot.CreatedTick,
            CreatedTimestamp = snapshot.CreatedTimestamp
        };
    }

    public static BsonDocument BuildPatchDocument(MarketOrderPatch patch)
    {
        ArgumentNullException.ThrowIfNull(patch);

        var document = new BsonDocument();
        if (patch.Active.HasValue)
            document[MarketOrderDocumentFields.Active] = patch.Active.Value;
        if (patch.Amount.HasValue)
            document[MarketOrderDocumentFields.Amount] = patch.Amount.Value;
        if (patch.RemainingAmount.HasValue)
            document[MarketOrderDocumentFields.RemainingAmount] = patch.RemainingAmount.Value;
        if (patch.TotalAmount.HasValue)
            document[MarketOrderDocumentFields.TotalAmount] = patch.TotalAmount.Value;
        if (patch.Price.HasValue)
            document[MarketOrderDocumentFields.Price] = patch.Price.Value;
        if (patch.CreatedTick.HasValue)
            document[MarketOrderDocumentFields.CreatedTick] = patch.CreatedTick.Value;
        if (patch.CreatedTimestamp.HasValue)
            document[MarketOrderDocumentFields.CreatedTimestamp] = patch.CreatedTimestamp.Value;
        if (patch.Type is not null)
            document[MarketOrderDocumentFields.Type] = patch.Type;
        if (patch.ResourceType is not null)
            document[MarketOrderDocumentFields.ResourceType] = patch.ResourceType;
        if (patch.RoomName is not null)
            document[MarketOrderDocumentFields.RoomName] = patch.RoomName;

        return document;
    }

    private static ObjectId ParseObjectId(string id)
    {
        if (ObjectId.TryParse(id, out var objectId))
            return objectId;

        return ObjectId.GenerateNewId();
    }
}
