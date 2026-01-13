namespace ScreepsDotNet.Backend.Cli.Tests.Integration;

using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Cli.Commands.Bot;
using ScreepsDotNet.Backend.Cli.Commands.Map;
using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Backend.Core.Models.Bots;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Storage.MongoRedis.Repositories;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;
using ScreepsDotNet.Storage.MongoRedis.Services;

public sealed class BotCommandsIntegrationTests(MongoMapIntegrationFixture fixture) : IClassFixture<MongoMapIntegrationFixture>
{
    [Fact]
    public async Task BotSpawnCommand_CreatesUserAndSpawn()
    {
        await fixture.ResetAsync();
        var roomName = "W41N41";
        var token = TestContext.Current.CancellationToken;
        await GenerateRoomAsync(roomName, token);

        var botProvider = new InMemoryBotDefinitionProvider("alpha");
        var service = CreateBotControlService(botProvider);
        var command = new BotSpawnCommand(service);
        var settings = new BotSpawnCommand.Settings
        {
            BotName = "alpha",
            RoomName = roomName,
            Username = "AlphaIntegration",
            Cpu = 150,
            GlobalControlLevel = 3
        };

        var exitCode = await command.ExecuteAsync(null!, settings, token);

        Assert.Equal(0, exitCode);

        var users = fixture.Database.GetCollection<UserDocument>("users");
        var user = await users.Find(document => document.Username == "AlphaIntegration").FirstOrDefaultAsync(token);
        Assert.NotNull(user);
        Assert.Equal("alpha", user!.Bot);

        var roomObjects = fixture.Database.GetCollection<BsonDocument>("rooms.objects");
        var spawnExists = await roomObjects
            .Find(doc => doc["room"] == roomName && doc["type"] == StructureType.Spawn.ToDocumentValue() && doc["user"] == user.Id)
            .AnyAsync(token);
        Assert.True(spawnExists);
    }

    [Fact]
    public async Task BotReloadCommand_ReplacesUserCodeBranch()
    {
        await fixture.ResetAsync();
        var roomName = "W42N42";
        var token = TestContext.Current.CancellationToken;
        await GenerateRoomAsync(roomName, token);

        var botProvider = new InMemoryBotDefinitionProvider("beta");
        var service = CreateBotControlService(botProvider);
        var spawnCommand = new BotSpawnCommand(service);
        var username = "BetaIntegration";
        await spawnCommand.ExecuteAsync(null!, new BotSpawnCommand.Settings
        {
            BotName = "beta",
            RoomName = roomName,
            Username = username
        }, token);

        var users = fixture.Database.GetCollection<UserDocument>("users");
        var user = await users.Find(doc => doc.Username == username).FirstOrDefaultAsync(token);
        Assert.NotNull(user);

        var userCode = fixture.Database.GetCollection<UserCodeDocument>("users.code");
        var before = await userCode.Find(doc => doc.UserId == user!.Id).FirstOrDefaultAsync(token);
        Assert.NotNull(before);

        var reloadCommand = new BotReloadCommand(service);
        var exitCode = await reloadCommand.ExecuteAsync(null!, new BotReloadCommand.Settings { BotName = "beta" }, token);
        Assert.Equal(0, exitCode);

        var after = await userCode.Find(doc => doc.UserId == user.Id).ToListAsync(token);
        Assert.Single(after);
        Assert.NotEqual(before!.Branch, after[0].Branch);
    }

    [Fact]
    public async Task BotRemoveCommand_DeletesUserArtifacts()
    {
        await fixture.ResetAsync();
        var roomName = "W43N43";
        var token = TestContext.Current.CancellationToken;
        await GenerateRoomAsync(roomName, token);

        var botProvider = new InMemoryBotDefinitionProvider("gamma");
        var service = CreateBotControlService(botProvider);
        var spawnCommand = new BotSpawnCommand(service);
        var username = "GammaIntegration";
        await spawnCommand.ExecuteAsync(null!, new BotSpawnCommand.Settings
        {
            BotName = "gamma",
            RoomName = roomName,
            Username = username
        }, token);

        var removeCommand = new BotRemoveCommand(service);
        var exitCode = await removeCommand.ExecuteAsync(null!, new BotRemoveCommand.Settings { Username = username }, token);
        Assert.Equal(0, exitCode);

        var users = fixture.Database.GetCollection<UserDocument>("users");
        var user = await users.Find(doc => doc.Username == username).FirstOrDefaultAsync(token);
        Assert.Null(user);

        var roomObjects = fixture.Database.GetCollection<BsonDocument>("rooms.objects");
        var spawnExists = await roomObjects.Find(doc => doc["room"] == roomName && doc["type"] == "spawn").AnyAsync(token);
        Assert.False(spawnExists);
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

    private MongoBotControlService CreateBotControlService(IBotDefinitionProvider provider)
    {
        var databaseProvider = fixture.DatabaseProvider;
        var memoryRepository = new MongoUserMemoryRepository(databaseProvider);
        var userWorldRepository = new MongoUserWorldRepository(databaseProvider);
        var respawnService = new MongoUserRespawnService(databaseProvider, userWorldRepository);
        var worldMetadataRepository = new MongoWorldMetadataRepository(databaseProvider);
        return new MongoBotControlService(databaseProvider,
                                          provider,
                                          memoryRepository,
                                          respawnService,
                                          worldMetadataRepository,
                                          NullLogger<MongoBotControlService>.Instance);
    }

    private sealed class InMemoryBotDefinitionProvider(string botName) : IBotDefinitionProvider
    {
        private readonly Dictionary<string, BotDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase)
        {
            [botName] = new BotDefinition(botName,
                                          $"Definition for {botName}",
                                          new Dictionary<string, string>(StringComparer.Ordinal)
                                          {
                                              ["main"] = "module.exports = {};"
                                          })
        };

        public Task<IReadOnlyList<BotDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<BotDefinition>>(_definitions.Values.ToList());

        public Task<BotDefinition?> FindDefinitionAsync(string name, CancellationToken cancellationToken = default)
        {
            _definitions.TryGetValue(name, out var definition);
            return Task.FromResult(definition);
        }
    }
}
