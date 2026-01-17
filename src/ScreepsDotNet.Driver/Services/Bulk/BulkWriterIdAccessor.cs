using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Driver.Constants;

namespace ScreepsDotNet.Driver.Services.Bulk;

internal readonly record struct BulkWriterIdAccessor<TDocument>()
    where TDocument : class
{
    public Func<TDocument, string?>? GetId { get; init; }
    public Action<TDocument, string>? AssignId { get; init; }
    public Func<string>? GenerateId { get; init; }
    public Func<string, FilterDefinition<TDocument>> CreateFilter { get; init; } = default!;
    public Func<string, BsonValue> CreateBsonValue { get; init; } = default!;
}

internal static class BulkWriterIdAccessors
{
    public static BulkWriterIdAccessor<TDocument> ForObjectId<TDocument>(
        Func<TDocument, ObjectId> getter,
        Action<TDocument, ObjectId>? setter = null)
        where TDocument : class
    {
        return new BulkWriterIdAccessor<TDocument>
        {
            GetId = entity => {
                var id = getter(entity);
                var result = id == ObjectId.Empty ? null : id.ToString();
                return result;
            },
            AssignId = setter is null
                ? null
                : (entity, value) => setter(entity, ObjectId.Parse(value)),
            GenerateId = () => ObjectId.GenerateNewId().ToString(),
            CreateFilter = id => Builders<TDocument>.Filter.Eq(MongoDocumentFields.Id, ObjectId.Parse(id)),
            CreateBsonValue = id => new BsonObjectId(ObjectId.Parse(id))
        };
    }

    public static BulkWriterIdAccessor<TDocument> ForStringId<TDocument>(
        Func<TDocument, string?> getter,
        Action<TDocument, string>? setter = null,
        bool allowGeneratedIds = false)
        where TDocument : class
    {
        return new BulkWriterIdAccessor<TDocument>
        {
            GetId = getter,
            AssignId = setter,
            GenerateId = allowGeneratedIds ? GuidGenerator : null,
            CreateFilter = id => Builders<TDocument>.Filter.Eq(MongoDocumentFields.Id, id),
            CreateBsonValue = id => new BsonString(id)
        };

        static string GuidGenerator()
            => ObjectId.GenerateNewId().ToString();
    }

    public static BulkWriterIdAccessor<BsonDocument> ForBsonDocument()
    {
        return new BulkWriterIdAccessor<BsonDocument>
        {
            GetId = document => {
                if (!document.TryGetValue(MongoDocumentFields.Id, out var value))
                    return null;
                return value switch
                {
                    BsonObjectId objectId => objectId.Value.ToString(),
                    BsonString bsonString => bsonString.Value,
                    _ => value.ToString()
                };
            },
            AssignId = (document, value) => document[MongoDocumentFields.Id] = CreateBsonValue(value),
            GenerateId = () => ObjectId.GenerateNewId().ToString(),
            CreateFilter = id => Builders<BsonDocument>.Filter.Eq(MongoDocumentFields.Id, CreateBsonValue(id)),
            CreateBsonValue = CreateBsonValue
        };

        static BsonValue CreateBsonValue(string id)
            => ObjectId.TryParse(id, out var objectId) ? new BsonObjectId(objectId) : new BsonString(id);
    }
}
