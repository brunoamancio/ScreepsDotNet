using MongoDB.Bson;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
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

    [Fact]
    public void MapRoomObject_MapsActionLogHealed()
    {
        var healedDoc = new BsonDocument
        {
            [RoomDocumentFields.RoomObject.ActionLogFields.X] = 10,
            [RoomDocumentFields.RoomObject.ActionLogFields.Y] = 20
        };

        var document = new RoomObjectDocument
        {
            Id = ObjectId.GenerateNewId(),
            Type = RoomObjectTypes.Creep,
            Room = "W1N1",
            X = 1,
            Y = 1,
            ActionLog = new BsonDocument
            {
                [RoomDocumentFields.RoomObject.ActionLogFields.Healed] = healedDoc
            }
        };

        var snapshot = RoomContractMapper.MapRoomObject(document);
        Assert.NotNull(snapshot.ActionLog);
        Assert.NotNull(snapshot.ActionLog!.Healed);
        Assert.Equal(10, snapshot.ActionLog.Healed!.X);
        Assert.Equal(20, snapshot.ActionLog.Healed!.Y);

        var roundtrip = RoomContractMapper.MapRoomObjectDocument(snapshot);
        Assert.True(roundtrip.ActionLog?.Contains(RoomDocumentFields.RoomObject.ActionLogFields.Healed));
    }

    [Fact]
    public void CreateRoomObjectPatchDocument_WritesActionLogHealed()
    {
        var patch = new RoomObjectPatchPayload
        {
            ActionLog = new RoomObjectActionLogPatch(
                Healed: new RoomObjectActionLogHealed(7, 8))
        };

        var document = RoomContractMapper.CreateRoomObjectPatchDocument(patch);
        Assert.True(document.Contains(RoomDocumentFields.RoomObject.ActionLog));

        var log = document[RoomDocumentFields.RoomObject.ActionLog].AsBsonDocument;
        var healed = log[RoomDocumentFields.RoomObject.ActionLogFields.Healed].AsBsonDocument;

        Assert.Equal(7, healed[RoomDocumentFields.RoomObject.ActionLogFields.X].AsInt32);
        Assert.Equal(8, healed[RoomDocumentFields.RoomObject.ActionLogFields.Y].AsInt32);
    }

    [Fact]
    public void MapRoomObject_MapsActionLogRepairAndBuild()
    {
        var document = new RoomObjectDocument
        {
            Id = ObjectId.GenerateNewId(),
            Type = RoomObjectTypes.Creep,
            Room = "W1N1",
            X = 2,
            Y = 2,
            ActionLog = new BsonDocument
            {
                [RoomDocumentFields.RoomObject.ActionLogFields.Repair] = new BsonDocument
                {
                    [RoomDocumentFields.RoomObject.ActionLogFields.X] = 4,
                    [RoomDocumentFields.RoomObject.ActionLogFields.Y] = 5
                },
                [RoomDocumentFields.RoomObject.ActionLogFields.Build] = new BsonDocument
                {
                    [RoomDocumentFields.RoomObject.ActionLogFields.X] = 6,
                    [RoomDocumentFields.RoomObject.ActionLogFields.Y] = 7
                }
            }
        };

        var snapshot = RoomContractMapper.MapRoomObject(document);
        Assert.Equal(4, snapshot.ActionLog!.Repair!.X);
        Assert.Equal(5, snapshot.ActionLog!.Repair!.Y);
        Assert.Equal(6, snapshot.ActionLog.Build!.X);
        Assert.Equal(7, snapshot.ActionLog.Build!.Y);

        var roundtrip = RoomContractMapper.MapRoomObjectDocument(snapshot);
        Assert.True(roundtrip.ActionLog!.Contains(RoomDocumentFields.RoomObject.ActionLogFields.Repair));
        Assert.True(roundtrip.ActionLog!.Contains(RoomDocumentFields.RoomObject.ActionLogFields.Build));
    }

    [Fact]
    public void MapRoomObject_MapsConstructionProgress()
    {
        var document = new RoomObjectDocument
        {
            Id = ObjectId.GenerateNewId(),
            Type = RoomObjectTypes.ConstructionSite,
            Room = "W1N1",
            X = 10,
            Y = 10,
            Progress = 150,
            ProgressTotal = 3000
        };

        var snapshot = RoomContractMapper.MapRoomObject(document);
        Assert.Equal(150, snapshot.Progress);
        Assert.Equal(3000, snapshot.ProgressTotal);

        var roundtrip = RoomContractMapper.MapRoomObjectDocument(snapshot);
        Assert.Equal(150, roundtrip.Progress);
        Assert.Equal(3000, roundtrip.ProgressTotal);
    }

    [Fact]
    public void MapRoomObject_MapsSourceAndMineralFields()
    {
        var document = new RoomObjectDocument
        {
            Id = ObjectId.GenerateNewId(),
            Type = RoomObjectTypes.Source,
            Room = "W1N1",
            X = 4,
            Y = 6,
            Energy = 2500,
            InvaderHarvested = 3,
            MineralType = ResourceTypes.Hydrogen,
            MineralAmount = 3000
        };

        var snapshot = RoomContractMapper.MapRoomObject(document);
        Assert.Equal(2500, snapshot.Energy);
        Assert.Equal(3, snapshot.InvaderHarvested);
        Assert.Equal(3000, snapshot.MineralAmount);

        var roundtrip = RoomContractMapper.MapRoomObjectDocument(snapshot);
        Assert.Equal(2500, roundtrip.Energy);
        Assert.Equal(3, roundtrip.InvaderHarvested);
        Assert.Equal(3000, roundtrip.MineralAmount);
    }

    [Fact]
    public void MapRoomObject_MapsDepositFields()
    {
        var document = new RoomObjectDocument
        {
            Id = ObjectId.GenerateNewId(),
            Type = RoomObjectTypes.Deposit,
            Room = "W2N2",
            X = 12,
            Y = 17,
            DepositType = ResourceTypes.Mist,
            Harvested = 42,
            Cooldown = 5,
            CooldownTime = 123456
        };

        var snapshot = RoomContractMapper.MapRoomObject(document);
        Assert.Equal(42, snapshot.Harvested);
        Assert.Equal(5, snapshot.Cooldown);
        Assert.Equal(123456, snapshot.CooldownTime);

        var roundtrip = RoomContractMapper.MapRoomObjectDocument(snapshot);
        Assert.Equal(42, roundtrip.Harvested);
        Assert.Equal(5, roundtrip.Cooldown);
        Assert.Equal(123456, roundtrip.CooldownTime);
    }

    [Fact]
    public void MapRoomObject_MapsActionLogHarvest()
    {
        var document = new RoomObjectDocument
        {
            Id = ObjectId.GenerateNewId(),
            Type = RoomObjectTypes.Creep,
            Room = "W3N3",
            X = 6,
            Y = 9,
            ActionLog = new BsonDocument
            {
                [RoomDocumentFields.RoomObject.ActionLogFields.Harvest] = new BsonDocument
                {
                    [RoomDocumentFields.RoomObject.ActionLogFields.X] = 11,
                    [RoomDocumentFields.RoomObject.ActionLogFields.Y] = 13
                }
            }
        };

        var snapshot = RoomContractMapper.MapRoomObject(document);
        Assert.NotNull(snapshot.ActionLog);
        Assert.Equal(11, snapshot.ActionLog!.Harvest!.X);
        Assert.Equal(13, snapshot.ActionLog.Harvest!.Y);

        var roundtrip = RoomContractMapper.MapRoomObjectDocument(snapshot);
        Assert.True(roundtrip.ActionLog!.Contains(RoomDocumentFields.RoomObject.ActionLogFields.Harvest));
    }
}
