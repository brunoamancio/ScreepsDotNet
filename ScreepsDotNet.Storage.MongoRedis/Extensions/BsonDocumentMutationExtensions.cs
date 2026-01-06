using MongoDB.Bson;

namespace ScreepsDotNet.Storage.MongoRedis.Extensions;

internal static class BsonDocumentMutationExtensions
{
    public static void SetValueAtPath(this BsonDocument document, IReadOnlyList<string> segments, BsonValue value)
    {
        if (segments.Count == 0)
            throw new ArgumentException("Segments cannot be empty.", nameof(segments));

        Set(document, segments, 0, value);
    }

    public static void RemoveValueAtPath(this BsonDocument document, IReadOnlyList<string> segments)
    {
        if (segments.Count == 0)
            return;

        Remove(document, segments, 0);
    }

    private static void Set(BsonDocument document, IReadOnlyList<string> segments, int index, BsonValue value)
    {
        while (true) {
            var key = segments[index];
            if (index == segments.Count - 1) {
                document[key] = value;
                return;
            }

            if (!document.TryGetValue(key, out var child) || !child.IsBsonDocument) {
                var newChild = new BsonDocument();
                document[key] = newChild;
                document = newChild;
                index += 1;
                continue;
            }

            document = child.AsBsonDocument;
            index += 1;
        }
    }

    private static void Remove(BsonDocument document, IReadOnlyList<string> segments, int index)
    {
        var key = segments[index];
        if (!document.TryGetValue(key, out var child))
            return;

        if (index == segments.Count - 1)
        {
            document.Remove(key);
            return;
        }

        if (child.IsBsonDocument)
        {
            var childDocument = child.AsBsonDocument;
            Remove(childDocument, segments, index + 1);
            if (!childDocument.Elements.Any())
                document.Remove(key);
        }
    }
}
