namespace ScreepsDotNet.Driver.Services.GlobalProcessing;

using MongoDB.Bson;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

internal static class PowerCreepDocumentMapper
{
    public static PowerCreepDocument ToDocument(PowerCreepSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var document = new PowerCreepDocument
        {
            Id = ParseObjectId(snapshot.Id),
            UserId = snapshot.UserId,
            Name = snapshot.Name,
            ClassName = snapshot.ClassName,
            Level = snapshot.Level,
            HitsMax = snapshot.HitsMax,
            Store = new Dictionary<string, int>(snapshot.Store, StringComparer.Ordinal),
            StoreCapacity = snapshot.StoreCapacity,
            SpawnCooldownTime = snapshot.SpawnCooldownTime,
            DeleteTime = snapshot.DeleteTime,
            Shard = snapshot.Shard,
            Powers = snapshot.Powers.ToDictionary(pair => pair.Key,
                                                  pair => new PowerCreepPowerDocument { Level = pair.Value.Level },
                                                  StringComparer.Ordinal)
        };

        return document;
    }

    private static ObjectId ParseObjectId(string id)
        => ObjectId.TryParse(id, out var objectId)
            ? objectId
            : ObjectId.GenerateNewId();
}
