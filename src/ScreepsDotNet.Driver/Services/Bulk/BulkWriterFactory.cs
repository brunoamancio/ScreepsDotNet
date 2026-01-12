using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Driver.Abstractions.Bulk;
using ScreepsDotNet.Storage.MongoRedis.Options;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

namespace ScreepsDotNet.Driver.Services.Bulk;

internal sealed class BulkWriterFactory(IMongoDatabaseProvider databaseProvider) : IBulkWriterFactory
{
    private MongoRedisStorageOptions Options => databaseProvider.Settings;

    public IBulkWriter<RoomObjectDocument> CreateRoomObjectsWriter()
        => CreateWriter(databaseProvider.GetCollection<RoomObjectDocument>(Options.RoomObjectsCollection),
            BulkWriterIdAccessors.ForObjectId<RoomObjectDocument>(doc => doc.Id, (doc, value) => doc.Id = value));

    public IBulkWriter<RoomFlagDocument> CreateRoomFlagsWriter()
        => CreateWriter(databaseProvider.GetCollection<RoomFlagDocument>(Options.RoomsFlagsCollection),
            BulkWriterIdAccessors.ForStringId<RoomFlagDocument>(doc => doc.Id, (doc, value) => doc.Id = value));

    public IBulkWriter<UserDocument> CreateUsersWriter()
        => CreateWriter(databaseProvider.GetCollection<UserDocument>(Options.UsersCollection),
            BulkWriterIdAccessors.ForStringId<UserDocument>(doc => doc.Id, (doc, value) => doc.Id = value));

    public IBulkWriter<RoomDocument> CreateRoomsWriter()
        => CreateWriter(databaseProvider.GetCollection<RoomDocument>(Options.RoomsCollection),
            BulkWriterIdAccessors.ForStringId<RoomDocument>(doc => doc.Id, (doc, value) => doc.Id = value));

    public IBulkWriter<BsonDocument> CreateTransactionsWriter()
        => CreateWriter(databaseProvider.GetCollection<BsonDocument>(Options.TransactionsCollection),
            BulkWriterIdAccessors.ForBsonDocument());

    public IBulkWriter<MarketOrderDocument> CreateMarketOrdersWriter()
        => CreateWriter(databaseProvider.GetCollection<MarketOrderDocument>(Options.MarketOrdersCollection),
            BulkWriterIdAccessors.ForObjectId<MarketOrderDocument>(doc => doc.Id, (doc, value) => doc.Id = value));

    public IBulkWriter<MarketOrderDocument> CreateMarketIntershardOrdersWriter()
        => CreateWriter(databaseProvider.GetCollection<MarketOrderDocument>(Options.MarketOrdersCollection),
            BulkWriterIdAccessors.ForObjectId<MarketOrderDocument>(doc => doc.Id, (doc, value) => doc.Id = value));

    public IBulkWriter<UserMoneyEntryDocument> CreateUsersMoneyWriter()
        => CreateWriter(databaseProvider.GetCollection<UserMoneyEntryDocument>(Options.UserMoneyCollection),
            BulkWriterIdAccessors.ForObjectId<UserMoneyEntryDocument>(doc => doc.Id, (doc, value) => doc.Id = value));

    public IBulkWriter<BsonDocument> CreateUsersResourcesWriter()
        => CreateWriter(databaseProvider.GetCollection<BsonDocument>(Options.UsersResourcesCollection),
            BulkWriterIdAccessors.ForBsonDocument());

    public IBulkWriter<PowerCreepDocument> CreateUsersPowerCreepsWriter()
        => CreateWriter(databaseProvider.GetCollection<PowerCreepDocument>(Options.UsersPowerCreepsCollection),
            BulkWriterIdAccessors.ForObjectId<PowerCreepDocument>(doc => doc.Id, (doc, value) => doc.Id = value));

    private static IBulkWriter<TDocument> CreateWriter<TDocument>(
        IMongoCollection<TDocument> collection,
        BulkWriterIdAccessor<TDocument> accessor)
        where TDocument : class
        => new BulkWriter<TDocument>(collection, accessor);
}
