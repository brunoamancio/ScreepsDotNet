namespace ScreepsDotNet.Backend.Cli.Tests.Integration;

using MongoDB.Driver;
using ScreepsDotNet.Backend.Cli.Commands.Map;
using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;
using Spectre.Console.Cli;

public sealed class MapCommandsIntegrationTests(MongoMapIntegrationFixture fixture) : IClassFixture<MongoMapIntegrationFixture>
{
    private readonly MongoMapIntegrationFixture _fixture = fixture;

    [Fact]
    public async Task MapGenerateCommand_PersistsRoomTerrainAndObjects()
    {
        await _fixture.ResetAsync();
        var service = _fixture.CreateService();
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

        var exitCode = await command.ExecuteAsync(null!, settings, CancellationToken.None);

        Assert.Equal(0, exitCode);

        var rooms = _fixture.GetCollection<RoomDocument>("rooms");
        var room = await rooms.Find(document => document.Id == roomName).FirstOrDefaultAsync();
        Assert.NotNull(room);
        Assert.Equal("normal", room.Status);

        var terrain = _fixture.GetCollection<RoomTerrainDocument>("rooms.terrain");
        var terrainDoc = await terrain.Find(document => document.Room == roomName).FirstOrDefaultAsync();
        Assert.NotNull(terrainDoc);
        Assert.Equal("terrain", terrainDoc.Type);
        Assert.NotNull(terrainDoc.Terrain);
        Assert.Equal(2500, terrainDoc.Terrain!.Length);

        var roomObjects = _fixture.GetCollection<RoomObjectDocument>("rooms.objects");
        var sources = await roomObjects.Find(document => document.Room == roomName && document.Type == "source")
                                       .CountDocumentsAsync();
        Assert.Equal(3, sources);
        var controllerExists = await roomObjects.Find(document => document.Room == roomName && document.Type == StructureType.Controller.ToDocumentValue())
                                                .AnyAsync();
        Assert.True(controllerExists);
    }

    [Fact]
    public async Task MapCloseAndOpenCommands_UpdateRoomStatus()
    {
        await _fixture.ResetAsync();
        var service = _fixture.CreateService();
        const string roomName = "W31N31";
        await new MapGenerateCommand(service).ExecuteAsync(null!, new MapGenerateCommand.Settings
        {
            RoomName = roomName,
            Overwrite = true
        }, CancellationToken.None);

        var closeCommand = new MapCloseCommand(service);
        await closeCommand.ExecuteAsync(null!, new MapCloseCommand.Settings { RoomName = roomName }, CancellationToken.None);

        var rooms = _fixture.GetCollection<RoomDocument>("rooms");
        var room = await rooms.Find(document => document.Id == roomName).FirstOrDefaultAsync();
        Assert.NotNull(room);
        Assert.Equal("closed", room.Status);

        var openCommand = new MapOpenCommand(service);
        await openCommand.ExecuteAsync(null!, new MapOpenCommand.Settings { RoomName = roomName }, CancellationToken.None);
        room = await rooms.Find(document => document.Id == roomName).FirstOrDefaultAsync();
        Assert.NotNull(room);
        Assert.Equal("normal", room.Status);
    }

    [Fact]
    public async Task MapRemoveCommand_DeletesRoomAndObjects()
    {
        await _fixture.ResetAsync();
        var service = _fixture.CreateService();
        const string roomName = "W32N32";
        await new MapGenerateCommand(service).ExecuteAsync(null!, new MapGenerateCommand.Settings
        {
            RoomName = roomName,
            Overwrite = true
        }, CancellationToken.None);

        var removeCommand = new MapRemoveCommand(service);
        await removeCommand.ExecuteAsync(null!, new MapRemoveCommand.Settings
        {
            RoomName = roomName,
            PurgeObjects = true
        }, CancellationToken.None);

        var rooms = _fixture.GetCollection<RoomDocument>("rooms");
        var room = await rooms.Find(document => document.Id == roomName).FirstOrDefaultAsync();
        Assert.Null(room);

        var roomObjects = _fixture.GetCollection<RoomObjectDocument>("rooms.objects");
        var remainingObjects = await roomObjects.Find(document => document.Room == roomName).AnyAsync();
        Assert.False(remainingObjects);
    }

    [Fact]
    public async Task MapTerrainRefreshCommand_NormalizesTerrainType()
    {
        await _fixture.ResetAsync();
        var service = _fixture.CreateService();
        const string roomName = "W33N33";
        await new MapGenerateCommand(service).ExecuteAsync(null!, new MapGenerateCommand.Settings
        {
            RoomName = roomName,
            Overwrite = true
        }, CancellationToken.None);

        var terrain = _fixture.GetCollection<RoomTerrainDocument>("rooms.terrain");
        await terrain.UpdateOneAsync(document => document.Room == roomName,
                                     Builders<RoomTerrainDocument>.Update.Unset(document => document.Type));

        var refreshCommand = new MapTerrainRefreshCommand(service);
        await refreshCommand.ExecuteAsync(null!, new MapTerrainRefreshCommand.Settings(), CancellationToken.None);

        var refreshed = await terrain.Find(document => document.Room == roomName).FirstOrDefaultAsync();
        Assert.NotNull(refreshed);
        Assert.Equal("terrain", refreshed!.Type);
    }

}
