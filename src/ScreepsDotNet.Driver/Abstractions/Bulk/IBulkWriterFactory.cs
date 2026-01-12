using MongoDB.Bson;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

namespace ScreepsDotNet.Driver.Abstractions.Bulk;

public interface IBulkWriterFactory
{
    IBulkWriter<RoomObjectDocument> CreateRoomObjectsWriter();
    IBulkWriter<RoomFlagDocument> CreateRoomFlagsWriter();
    IBulkWriter<UserDocument> CreateUsersWriter();
    IBulkWriter<RoomDocument> CreateRoomsWriter();
    IBulkWriter<BsonDocument> CreateTransactionsWriter();
    IBulkWriter<MarketOrderDocument> CreateMarketOrdersWriter();
    IBulkWriter<MarketOrderDocument> CreateMarketIntershardOrdersWriter();
    IBulkWriter<UserMoneyEntryDocument> CreateUsersMoneyWriter();
    IBulkWriter<BsonDocument> CreateUsersResourcesWriter();
    IBulkWriter<PowerCreepDocument> CreateUsersPowerCreepsWriter();
}
