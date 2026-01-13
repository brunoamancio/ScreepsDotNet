namespace ScreepsDotNet.Backend.Cli.Tests.Integration;

using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Cli.Commands.Map;
using ScreepsDotNet.Backend.Cli.Commands.Stronghold;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Storage.MongoRedis.Repositories;
using ScreepsDotNet.Storage.MongoRedis.Services;

public sealed class StrongholdCommandsIntegrationTests(MongoMapIntegrationFixture fixture) : IClassFixture<MongoMapIntegrationFixture>
{
    [Fact]
    public async Task StrongholdSpawnCommand_CreatesInvaderCore()
    {
        await fixture.ResetAsync();
        var roomName = "W44N44";
        var token = TestContext.Current.CancellationToken;
        await GenerateRoomAsync(roomName, token);

        var service = CreateStrongholdService();
        var command = new StrongholdSpawnCommand(service);

        var exitCode = await command.ExecuteAsync(null!, new StrongholdSpawnCommand.Settings
        {
            RoomName = roomName,
            TemplateName = "bunker1"
        }, token);

        Assert.Equal(0, exitCode);

        var roomObjects = fixture.Database.GetCollection<BsonDocument>("rooms.objects");
        var core = await roomObjects.Find(doc => doc["room"] == roomName && doc["type"] == "invaderCore")
                                    .FirstOrDefaultAsync(token);

        Assert.NotNull(core);
        Assert.False(string.IsNullOrWhiteSpace(core!["strongholdId"].AsString));
        Assert.Equal("bunker1", core["templateName"]);
    }

    [Fact]
    public async Task StrongholdExpandCommand_UpdatesNextExpandTime()
    {
        await fixture.ResetAsync();
        var roomName = "W45N45";
        var token = TestContext.Current.CancellationToken;
        await GenerateRoomAsync(roomName, token);

        var service = CreateStrongholdService();
        var spawnCommand = new StrongholdSpawnCommand(service);
        await spawnCommand.ExecuteAsync(null!, new StrongholdSpawnCommand.Settings
        {
            RoomName = roomName,
            TemplateName = "bunker2"
        }, token);

        var roomObjects = fixture.Database.GetCollection<BsonDocument>("rooms.objects");
        var coreFilter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("room", roomName),
            Builders<BsonDocument>.Filter.Eq("type", "invaderCore"));
        var coreBefore = await roomObjects.Find(coreFilter).FirstOrDefaultAsync(token);
        Assert.NotNull(coreBefore);
        var previousNextExpand = coreBefore!["nextExpandTime"].AsInt32;

        var expandCommand = new StrongholdExpandCommand(service);
        var exitCode = await expandCommand.ExecuteAsync(null!, new StrongholdExpandCommand.Settings { RoomName = roomName }, token);
        Assert.Equal(0, exitCode);

        var coreAfter = await roomObjects.Find(coreFilter).FirstOrDefaultAsync(token);
        Assert.NotNull(coreAfter);
        Assert.NotEqual(previousNextExpand, coreAfter!["nextExpandTime"].AsInt32);
    }

    private async Task GenerateRoomAsync(string roomName, CancellationToken token)
    {
        var mapCommand = new MapGenerateCommand(fixture.MapControlService);
        await mapCommand.ExecuteAsync(null!, new MapGenerateCommand.Settings
        {
            RoomName = roomName,
            Overwrite = true
        }, token);
    }

    private IStrongholdControlService CreateStrongholdService()
    {
        var templateProvider = new EmbeddedStrongholdTemplateProvider();
        var worldMetadataRepository = new MongoWorldMetadataRepository(fixture.DatabaseProvider);
        return new MongoStrongholdControlService(fixture.DatabaseProvider, templateProvider, worldMetadataRepository);
    }
}
