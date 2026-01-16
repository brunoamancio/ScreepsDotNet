using MongoDB.Bson;
using ScreepsDotNet.Common;
using ScreepsDotNet.Driver.Constants;
using ScreepsDotNet.Driver.Contracts;

namespace ScreepsDotNet.Driver.Services.Rooms;

internal static class SpawnIntentMapper
{
    public static bool TryMap(BsonDocument document, out SpawnIntentEnvelope envelope)
    {
        envelope = default!;

        var create = TryMapCreateCreep(document.TryGetValue(IntentDocumentFields.Spawn.CreateCreep, out var createValue) ? createValue as BsonDocument : null);
        var renew = TryMapTargetIntent(document.TryGetValue(IntentDocumentFields.Spawn.RenewCreep, out var renewValue) ? renewValue as BsonDocument : null, static id => new RenewCreepIntent(id));
        var recycle = TryMapTargetIntent(document.TryGetValue(IntentDocumentFields.Spawn.RecycleCreep, out var recycleValue) ? recycleValue as BsonDocument : null, static id => new RecycleCreepIntent(id));
        var directions = TryMapSetSpawnDirections(document.TryGetValue(IntentDocumentFields.Spawn.SetSpawnDirections, out var directionsValue) ? directionsValue as BsonDocument : null);
        var cancel = document.Contains(IntentDocumentFields.Spawn.CancelSpawning);

        if (create is null && renew is null && recycle is null && directions is null && !cancel)
            return false;

        envelope = new SpawnIntentEnvelope(create, renew, recycle, directions, cancel);
        return true;
    }

    private static CreateCreepIntent? TryMapCreateCreep(BsonDocument? document)
    {
        if (document is null)
            return null;

        var name = document.TryGetString(IntentDocumentFields.Spawn.Name);
        if (string.IsNullOrWhiteSpace(name))
            return null;

        if (!document.TryGetValue(IntentDocumentFields.Spawn.Body, out var bodyValue) || bodyValue is not BsonArray bodyArray)
            return null;

        var bodyParts = ExtractStringArray(bodyArray);
        if (bodyParts.Count == 0)
            return null;

        IReadOnlyList<int>? directions = null;
        if (document.TryGetValue(IntentDocumentFields.Spawn.Directions, out var directionsValue) && directionsValue is BsonArray directionsArray)
        {
            var parsedDirections = ExtractDirectionArray(directionsArray);
            directions = parsedDirections.Count == 0 ? null : parsedDirections;
        }

        IReadOnlyList<string>? energyStructures = null;
        if (document.TryGetValue(IntentDocumentFields.Spawn.EnergyStructures, out var energyValue) && energyValue is BsonArray energyArray)
        {
            var parsed = ExtractStringArray(energyArray);
            energyStructures = parsed.Count == 0 ? null : parsed;
        }

        return new CreateCreepIntent(name, bodyParts, directions, energyStructures);
    }

    private static TIntent? TryMapTargetIntent<TIntent>(BsonDocument? document, Func<string, TIntent> factory)
        where TIntent : class
    {
        if (document is null)
            return null;

        var targetId = document.TryGetString(IntentKeys.TargetId);
        if (string.IsNullOrWhiteSpace(targetId))
            return null;

        return factory(targetId);
    }

    private static SetSpawnDirectionsIntent? TryMapSetSpawnDirections(BsonDocument? document)
    {
        if (document is null)
            return null;

        if (!document.TryGetValue(IntentDocumentFields.Spawn.Directions, out var directionsValue) || directionsValue is not BsonArray directionsArray)
            return null;

        var directions = ExtractDirectionArray(directionsArray);
        return directions.Count == 0 ? null : new SetSpawnDirectionsIntent(directions);
    }

    private static List<string> ExtractStringArray(BsonArray array)
    {
        var result = new List<string>(array.Count);
        foreach (var value in array)
        {
            if (value is not BsonString bsonString)
                continue;
            var str = bsonString.Value;
            if (!string.IsNullOrWhiteSpace(str))
                result.Add(str);
        }

        return result;
    }

    private static List<int> ExtractDirectionArray(BsonArray array)
    {
        var result = new List<int>(array.Count);
        var seen = new HashSet<int>();
        foreach (var value in array)
        {
            if (!value.IsInt32)
                continue;
            var direction = value.AsInt32;
            if (direction is < 1 or > 8)
                continue;
            if (seen.Add(direction))
                result.Add(direction);
        }

        return result;
    }

    private static string? TryGetString(this BsonDocument document, string field)
        => document.TryGetValue(field, out var value) && value is BsonString str
            ? str.Value
            : null;
}
