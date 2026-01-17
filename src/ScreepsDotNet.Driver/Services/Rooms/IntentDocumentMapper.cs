namespace ScreepsDotNet.Driver.Services.Rooms;

using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using ScreepsDotNet.Common.Extensions;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;

internal static class IntentDocumentMapper
{
    public static IReadOnlyList<IntentRecord> MapIntentRecords(BsonDocument? document)
    {
        if (document is null || document.ElementCount == 0)
            return [];

        var result = new IntentRecord[document.ElementCount];
        var index = 0;
        foreach (var element in document) {
            var arguments = MapIntentValue(element.Value);
            result[index++] = new IntentRecord(element.Name, arguments);
        }

        return result;
    }

    public static BsonDocument ToBsonDocument(IReadOnlyList<IntentRecord> records)
    {
        var document = new BsonDocument();
        if (records is null)
            return document;

        foreach (var record in records) {
            if (record.Arguments.Count == 0) {
                document[record.Name] = new BsonDocument();
                continue;
            }

            if (record.Arguments.Count == 1) {
                document[record.Name] = ToBsonDocument(record.Arguments[0]);
                continue;
            }

            var array = new BsonArray(record.Arguments.Count);
            foreach (var argument in record.Arguments)
                array.Add(ToBsonDocument(argument));

            document[record.Name] = array;
        }

        return document;
    }

    private static IReadOnlyList<IntentArgument> MapIntentValue(BsonValue value)
    {
        if (value is BsonArray array)
            return MapIntentArray(array);

        if (value is BsonDocument document)
            return [MapIntentArgument(document)];

        return [];
    }

    private static IReadOnlyList<IntentArgument> MapIntentArray(BsonArray array)
    {
        if (array.Count == 0)
            return [];

        var result = new IntentArgument[array.Count];
        for (var i = 0; i < array.Count; i++) {
            result[i] = array[i] is BsonDocument document
                ? MapIntentArgument(document)
                : new IntentArgument(new Dictionary<string, IntentFieldValue>(0, StringComparer.Ordinal));
        }

        return result;
    }

    private static IntentArgument MapIntentArgument(BsonDocument document)
    {
        if (document.ElementCount == 0)
            return new IntentArgument(new Dictionary<string, IntentFieldValue>(0, StringComparer.Ordinal));

        var fields = new Dictionary<string, IntentFieldValue>(document.ElementCount, StringComparer.Ordinal);
        foreach (var element in document)
            fields[element.Name] = ConvertIntentFieldValue(element.Value);

        return new IntentArgument(fields);
    }

    private static IntentFieldValue ConvertIntentFieldValue(BsonValue value)
        => value.BsonType switch
        {
            BsonType.Boolean => new IntentFieldValue(IntentFieldValueKind.Boolean, BooleanValue: value.AsBoolean),
            BsonType.Int32 => new IntentFieldValue(IntentFieldValueKind.Number, NumberValue: value.AsInt32),
            BsonType.Int64 => new IntentFieldValue(IntentFieldValueKind.Number, NumberValue: checked((int)value.AsInt64)),
            BsonType.Double => new IntentFieldValue(IntentFieldValueKind.Number, NumberValue: (int)Math.Round(value.AsDouble)),
            BsonType.Decimal128 => new IntentFieldValue(IntentFieldValueKind.Number, NumberValue: (int)Math.Round((double)value.AsDecimal128)),
            BsonType.String => new IntentFieldValue(IntentFieldValueKind.Text, TextValue: value.AsString),
            BsonType.Array => ConvertIntentArrayValue(value.AsBsonArray),
            BsonType.EndOfDocument => throw new NotImplementedException(),
            BsonType.Document => throw new NotImplementedException(),
            BsonType.Binary => throw new NotImplementedException(),
            BsonType.Undefined => throw new NotImplementedException(),
            BsonType.ObjectId => throw new NotImplementedException(),
            BsonType.DateTime => throw new NotImplementedException(),
            BsonType.Null => throw new NotImplementedException(),
            BsonType.RegularExpression => throw new NotImplementedException(),
            BsonType.JavaScript => throw new NotImplementedException(),
            BsonType.Symbol => throw new NotImplementedException(),
            BsonType.JavaScriptWithScope => throw new NotImplementedException(),
            BsonType.Timestamp => throw new NotImplementedException(),
            BsonType.MinKey => throw new NotImplementedException(),
            BsonType.MaxKey => throw new NotImplementedException(),
            _ => new IntentFieldValue(IntentFieldValueKind.Text, TextValue: value.ToString())
        };

    private static IntentFieldValue ConvertIntentArrayValue(BsonArray array)
    {
        if (array.Count == 0)
            return new IntentFieldValue(IntentFieldValueKind.TextArray, TextValues: []);

        if (array.All(v => v is BsonInt32 or BsonInt64 or BsonDouble or BsonDecimal128)) {
            var numbers = new int[array.Count];
            for (var i = 0; i < array.Count; i++)
                numbers[i] = ConvertIntentNumber(array[i]);
            return new IntentFieldValue(IntentFieldValueKind.NumberArray, NumberValues: numbers);
        }

        if (array.All(v => v.BsonType == BsonType.String)) {
            var textValues = new List<string>(array.Count);
            var bodyParts = new BodyPartType[array.Count];
            var allBodyParts = true;

            for (var i = 0; i < array.Count; i++) {
                var value = array[i].AsString ?? string.Empty;
                textValues.Add(value);
                if (value.TryParseBodyPartType(out var part))
                    bodyParts[i] = part;
                else
                    allBodyParts = false;
            }

            if (allBodyParts)
                return new IntentFieldValue(IntentFieldValueKind.BodyPartArray, BodyParts: bodyParts);

            return new IntentFieldValue(IntentFieldValueKind.TextArray, TextValues: textValues);
        }

        var fallback = new List<string>(array.Count);
        foreach (var item in array)
            fallback.Add(item.ToString() ?? string.Empty);

        return new IntentFieldValue(IntentFieldValueKind.TextArray, TextValues: fallback);
    }

    private static int ConvertIntentNumber(BsonValue value)
        => value.BsonType switch
        {
            BsonType.Int32 => value.AsInt32,
            BsonType.Int64 => checked((int)value.AsInt64),
            BsonType.Double => (int)Math.Round(value.AsDouble),
            BsonType.Decimal128 => (int)Math.Round((double)value.AsDecimal128),
            BsonType.EndOfDocument => throw new NotImplementedException(),
            BsonType.String => throw new NotImplementedException(),
            BsonType.Document => throw new NotImplementedException(),
            BsonType.Array => throw new NotImplementedException(),
            BsonType.Binary => throw new NotImplementedException(),
            BsonType.Undefined => throw new NotImplementedException(),
            BsonType.ObjectId => throw new NotImplementedException(),
            BsonType.Boolean => throw new NotImplementedException(),
            BsonType.DateTime => throw new NotImplementedException(),
            BsonType.Null => throw new NotImplementedException(),
            BsonType.RegularExpression => throw new NotImplementedException(),
            BsonType.JavaScript => throw new NotImplementedException(),
            BsonType.Symbol => throw new NotImplementedException(),
            BsonType.JavaScriptWithScope => throw new NotImplementedException(),
            BsonType.Timestamp => throw new NotImplementedException(),
            BsonType.MinKey => throw new NotImplementedException(),
            BsonType.MaxKey => throw new NotImplementedException(),
            _ => 0
        };

    private static BsonDocument ToBsonDocument(IntentArgument argument)
    {
        var document = new BsonDocument();
        foreach (var (field, value) in argument.Fields)
            document[field] = ConvertFieldValue(value);

        return document;
    }

    private static BsonValue ConvertFieldValue(IntentFieldValue value)
        => value.Kind switch
        {
            IntentFieldValueKind.Text => BsonValue.Create(value.TextValue ?? string.Empty),
            IntentFieldValueKind.Number => new BsonInt32(value.NumberValue ?? 0),
            IntentFieldValueKind.Boolean => BsonValue.Create(value.BooleanValue.GetValueOrDefault()),
            IntentFieldValueKind.TextArray => value.TextValues is null
                ? new BsonArray()
                : new BsonArray(value.TextValues.Select(text => BsonValue.Create(text ?? string.Empty))),
            IntentFieldValueKind.NumberArray => value.NumberValues is null
                ? new BsonArray()
                : new BsonArray(value.NumberValues.Select(n => new BsonInt32(n))),
            IntentFieldValueKind.BodyPartArray => value.BodyParts is null
                ? []
                : new BsonArray(value.BodyParts.Select(part => part.ToDocumentValue())),
            _ => BsonNull.Value
        };
}
