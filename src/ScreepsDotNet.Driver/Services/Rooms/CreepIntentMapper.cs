using MongoDB.Bson;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Constants;
using ScreepsDotNet.Driver.Contracts;

namespace ScreepsDotNet.Driver.Services.Rooms;

internal static class CreepIntentMapper
{
    public static bool TryMap(BsonDocument document, out CreepIntentEnvelope envelope)
    {
        envelope = new CreepIntentEnvelope(
            Move: TryMapMove(document),
            Attack: TryMapAttack(document, IntentKeys.Attack),
            RangedAttack: TryMapAttack(document, IntentKeys.RangedAttack),
            Heal: TryMapHeal(document),
            AdditionalFields: ExtractResidual(document));

        return envelope.Move is not null || envelope.Attack is not null || envelope.RangedAttack is not null || envelope.Heal is not null || envelope.AdditionalFields.Count > 0;
    }

    private static MoveIntent? TryMapMove(BsonDocument document)
    {
        if (!document.TryGetValue(IntentKeys.Move, out var moveValue) || moveValue is not BsonDocument moveDoc)
            return null;

        if (!moveDoc.TryGetValue(IntentDocumentFields.Movement.X, out var xValue) || !moveDoc.TryGetValue(IntentDocumentFields.Movement.Y, out var yValue))
            return null;

        if (!xValue.IsInt32 || !yValue.IsInt32)
            return null;

        return new MoveIntent(xValue.AsInt32, yValue.AsInt32);
    }

    private static AttackIntent? TryMapAttack(BsonDocument document, string field)
    {
        if (!document.TryGetValue(field, out var attackValue) || attackValue is not BsonDocument attackDoc)
            return null;

        if (!attackDoc.TryGetValue(IntentKeys.TargetId, out var targetValue) || targetValue is not BsonString targetStr)
            return null;

        var damage = attackDoc.TryGetValue(IntentKeys.Damage, out var damageValue) && damageValue.IsInt32
            ? damageValue.AsInt32
            : (int?)null;

        return new AttackIntent(targetStr.Value, damage);
    }

    private static HealIntent? TryMapHeal(BsonDocument document)
    {
        if (!document.TryGetValue(IntentKeys.Heal, out var healValue) || healValue is not BsonDocument healDoc)
            return null;

        if (!healDoc.TryGetValue(IntentKeys.TargetId, out var targetValue) || targetValue is not BsonString targetStr)
            return null;

        var amount = healDoc.TryGetValue(IntentKeys.Amount, out var amountValue) && amountValue.IsInt32
            ? amountValue.AsInt32
            : (int?)null;

        return new HealIntent(targetStr.Value, amount);
    }

    private static IReadOnlyDictionary<string, object?> ExtractResidual(BsonDocument document)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var element in document.Elements) {
            if (element.Name is IntentKeys.Move or IntentKeys.Attack or IntentKeys.RangedAttack or IntentKeys.Heal)
                continue;

            result[element.Name] = ConvertBsonValue(element.Value);
        }

        return result;
    }

    private static object? ConvertBsonValue(BsonValue value)
    {
        if (value.IsBsonNull)
            return null;

        return value.BsonType switch
        {
            BsonType.Boolean => value.AsBoolean,
            BsonType.Int32 => value.AsInt32,
            BsonType.Int64 => value.AsInt64,
            BsonType.Double => value.AsDouble,
            BsonType.String => value.AsString,
            BsonType.Document => ExtractDocument(value.AsBsonDocument),
            BsonType.Array => ExtractArray(value.AsBsonArray),
            _ => value.ToString()
        };
    }

    private static IReadOnlyDictionary<string, object?> ExtractDocument(BsonDocument document)
    {
        var result = new Dictionary<string, object?>(document.ElementCount, StringComparer.Ordinal);
        foreach (var element in document.Elements)
            result[element.Name] = ConvertBsonValue(element.Value);
        return result;
    }

    private static IReadOnlyList<object?> ExtractArray(BsonArray array)
    {
        var result = new List<object?>(array.Count);
        foreach (var value in array)
            result.Add(ConvertBsonValue(value));
        return result;
    }
}
