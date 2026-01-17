namespace ScreepsDotNet.Driver.Services.GlobalProcessing;

using System.Collections.Generic;
using MongoDB.Bson;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

internal static class UserMoneyEntryDocumentMapper
{
    public static UserMoneyEntryDocument ToDocument(UserMoneyLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var document = new UserMoneyEntryDocument
        {
            Id = ObjectId.GenerateNewId(),
            UserId = entry.UserId,
            Date = entry.TimestampUtc,
            ExtraElements = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["tick"] = entry.Tick,
                ["type"] = entry.Type,
                ["balance"] = entry.Balance,
                ["change"] = entry.Change,
                ["metadata"] = entry.Metadata
            }
        };

        return document;
    }
}
