using MongoDB.Bson;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Services.Rooms;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

namespace ScreepsDotNet.Driver.Tests.Rooms;

public sealed class RoomContractMapperTests
{
    [Fact]
    public void MapRoomObject_MapsSpawningPayload()
    {
        Direction[] directions = [Direction.Top, Direction.Right, Direction.Bottom];

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
                [RoomDocumentFields.RoomObject.SpawningFields.Directions] = new BsonArray(directions.Select(direction => (int)direction))
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

    [Fact]
    public void MapRoomObject_MapsIsSpawningFlagForCreep()
    {
        var document = new RoomObjectDocument
        {
            Id = ObjectId.GenerateNewId(),
            Type = RoomObjectTypes.Creep,
            Room = "W1N1",
            X = 5,
            Y = 5,
            Name = "creep1",
            Spawning = BsonBoolean.True
        };

        var snapshot = RoomContractMapper.MapRoomObject(document);
        Assert.True(snapshot.IsSpawning);

        var roundtrip = RoomContractMapper.MapRoomObjectDocument(snapshot);
        Assert.True(roundtrip.Spawning is BsonBoolean flag && flag.Value);
    }
}
