namespace ScreepsDotNet.Driver.Services.Rooms;

using System;
using MongoDB.Bson;
using ScreepsDotNet.Driver.Contracts;

/// <summary>
/// Driver-only wrapper that allows legacy BSON patch documents to travel through the mutation batch.
/// </summary>
internal sealed class BsonRoomObjectPatchPayload : IRoomObjectPatchPayload
{
    public BsonRoomObjectPatchPayload(BsonDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        Document = document.DeepClone().AsBsonDocument;
    }

    public BsonDocument Document { get; }
}
