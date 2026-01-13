namespace ScreepsDotNet.Backend.Cli.Tests.Integration;

using MongoDB.Driver;
using ScreepsDotNet.Backend.Cli.Commands.Map;
using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

public sealed class MapCommandsIntegrationTests(MongoMapIntegrationFixture fixture) : IClassFixture<MongoMapIntegrationFixture>
{
    [Fact]
    public async Task MapGenerateCommand_PersistsRoomTerrainAndObjects()
    {
        await fixture.ResetAsync();
        var token = TestContext.Current.CancellationToken;
        var service = fixture.CreateService();
        var command = new MapGenerateCommand(service);
        const string roomName = "W30N30";

        var settings = new MapGenerateCommand.Settings
        {
            RoomName = roomName,
            SourceCount = 3,
            KeeperLairs = true,
            Overwrite = true,
            Seed = 42
        };

        var exitCode = await command.ExecuteAsync(null!, settings, token);

        Assert.Equal(0, exitCode);

        var rooms = fixture.GetCollection<RoomDocument>("rooms");
        var room = await rooms.Find(document => document.Id == roomName).FirstOrDefaultAsync(token);
        Assert.NotNull(room);
        Assert.Equal("normal", room.Status);

        var terrain = fixture.GetCollection<RoomTerrainDocument>("rooms.terrain");
        var terrainDoc = await terrain.Find(document => document.Room == roomName).FirstOrDefaultAsync(token);
        Assert.NotNull(terrainDoc);
        Assert.Equal("terrain", terrainDoc.Type);
        Assert.NotNull(terrainDoc.Terrain);
        Assert.Equal(2500, terrainDoc.Terrain!.Length);

        var roomObjects = fixture.GetCollection<RoomObjectDocument>("rooms.objects");
        var sources = await roomObjects.Find(document => document.Room == roomName && document.Type == "source")
                                       .CountDocumentsAsync(token);
        Assert.Equal(3, sources);
        var controllerExists = await roomObjects.Find(document => document.Room == roomName && document.Type == StructureType.Controller.ToDocumentValue())
                                                .AnyAsync(token);
        Assert.True(controllerExists);
    }

    [Fact]
    public async Task MapCloseAndOpenCommands_UpdateRoomStatus()
    {
        await fixture.ResetAsync();
        var token = TestContext.Current.CancellationToken;
        var service = fixture.CreateService();
        const string roomName = "W31N31";
        await new MapGenerateCommand(service).ExecuteAsync(null!, new MapGenerateCommand.Settings
        {
            RoomName = roomName,
            Overwrite = true
        }, token);

        var closeCommand = new MapCloseCommand(service);
        await closeCommand.ExecuteAsync(null!, new MapCloseCommand.Settings { RoomName = roomName }, token);

        var rooms = fixture.GetCollection<RoomDocument>("rooms");
        var room = await rooms.Find(document => document.Id == roomName).FirstOrDefaultAsync(token);
        Assert.NotNull(room);
        Assert.Equal("closed", room.Status);

        var openCommand = new MapOpenCommand(service);
        await openCommand.ExecuteAsync(null!, new MapOpenCommand.Settings { RoomName = roomName }, token);
        room = await rooms.Find(document => document.Id == roomName).FirstOrDefaultAsync(token);
        Assert.NotNull(room);
        Assert.Equal("normal", room.Status);
    }

    [Fact]
    public async Task MapRemoveCommand_DeletesRoomAndObjects()
    {
        await fixture.ResetAsync();
        var token = TestContext.Current.CancellationToken;
        var service = fixture.CreateService();
        const string roomName = "W32N32";
        await new MapGenerateCommand(service).ExecuteAsync(null!, new MapGenerateCommand.Settings
        {
            RoomName = roomName,
            Overwrite = true
        }, token);

        var removeCommand = new MapRemoveCommand(service);
        await removeCommand.ExecuteAsync(null!, new MapRemoveCommand.Settings
        {
            RoomName = roomName,
            PurgeObjects = true
        }, token);

        var rooms = fixture.GetCollection<RoomDocument>("rooms");
        var room = await rooms.Find(document => document.Id == roomName).FirstOrDefaultAsync(token);
        Assert.Null(room);

        var roomObjects = fixture.GetCollection<RoomObjectDocument>("rooms.objects");
        var remainingObjects = await roomObjects.Find(document => document.Room == roomName).AnyAsync(token);
        Assert.False(remainingObjects);
    }

    [Fact]
    public async Task MapTerrainRefreshCommand_NormalizesTerrainType()
    {
        await fixture.ResetAsync();
        var token = TestContext.Current.CancellationToken;
        var service = fixture.CreateService();
        const string roomName = "W33N33";
        await new MapGenerateCommand(service).ExecuteAsync(null!, new MapGenerateCommand.Settings
        {
            RoomName = roomName,
            Overwrite = true
        }, token);

        var terrain = fixture.GetCollection<RoomTerrainDocument>("rooms.terrain");
        await terrain.UpdateOneAsync(document => document.Room == roomName,
                                     Builders<RoomTerrainDocument>.Update.Unset(document => document.Type),
                                     cancellationToken: token);

        var refreshCommand = new MapTerrainRefreshCommand(service);
        await refreshCommand.ExecuteAsync(null!, new MapTerrainRefreshCommand.Settings(), token);

        var refreshed = await terrain.Find(document => document.Room == roomName).FirstOrDefaultAsync(token);
        Assert.NotNull(refreshed);
        Assert.Equal("terrain", refreshed!.Type);
    }

    [Fact]
    public async Task MapAssetsUpdateCommand_Completes()
    {
        await fixture.ResetAsync();
        var token = TestContext.Current.CancellationToken;
        var service = fixture.CreateService();

        var command = new MapAssetsUpdateCommand(service);
        var settings = new MapAssetsUpdateCommand.Settings
        {
            RoomName = SeedDataDefaults.World.StartRoom,
            Full = true
        };

        var exitCode = await command.ExecuteAsync(null!, settings, token);

        Assert.Equal(0, exitCode);
    }

}
