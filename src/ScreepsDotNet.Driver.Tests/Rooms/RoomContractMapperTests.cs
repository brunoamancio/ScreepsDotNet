using MongoDB.Bson;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Services.Rooms;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

namespace ScreepsDotNet.Driver.Tests.Rooms;

public sealed class RoomContractMapperTests
{
    [Fact]
    public void MapRoomObject_MapsSpawningPayload()
    {
        int[] directions = [1, 3, 5];

        var document = new RoomObjectDocument
        {
            Id = ObjectId.GenerateNewId(),
            Type = RoomObjectTypes.Spawn,
            Room = "W1N1",
            X = 10,
            Y = 20,
            Spawning = new BsonDocument
            {
                [RoomDocumentFields.RoomObject.SpawningFields.Name] = "builder1",
                [RoomDocumentFields.RoomObject.SpawningFields.NeedTime] = 9,
                [RoomDocumentFields.RoomObject.SpawningFields.SpawnTime] = 12345,
                [RoomDocumentFields.RoomObject.SpawningFields.Directions] = new BsonArray(directions)
            }
        };

        var snapshot = RoomContractMapper.MapRoomObject(document);

        Assert.NotNull(snapshot.Spawning);
        Assert.Equal("builder1", snapshot.Spawning!.Name);
        Assert.Equal(9, snapshot.Spawning.NeedTime);
        Assert.Equal(12345, snapshot.Spawning.SpawnTime);
        Assert.Equal(directions, snapshot.Spawning.Directions);

        var roundtrip = RoomContractMapper.MapRoomObjectDocument(snapshot);
        Assert.True(roundtrip.Spawning is BsonDocument bson);
        Assert.Equal("builder1", roundtrip.Spawning!["name"].AsString);
        Assert.Equal(9, roundtrip.Spawning!["needTime"].AsInt32);
    }
}
