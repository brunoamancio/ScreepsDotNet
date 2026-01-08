namespace ScreepsDotNet.Backend.Cli.Tests.Integration;

using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Cli.Commands.Bot;
using ScreepsDotNet.Backend.Cli.Commands.Map;
using ScreepsDotNet.Backend.Core.Models.Bots;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Storage.MongoRedis.Repositories;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;
using ScreepsDotNet.Storage.MongoRedis.Services;

public sealed class BotCommandsIntegrationTests(MongoMapIntegrationFixture fixture) : IClassFixture<MongoMapIntegrationFixture>
{
    private readonly MongoMapIntegrationFixture _fixture = fixture;

    [Fact]
    public async Task BotSpawnCommand_CreatesUserAndSpawn()
    {
        await _fixture.ResetAsync();
        var roomName = "W41N41";
        await GenerateRoomAsync(roomName);

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

        var exitCode = await command.ExecuteAsync(null!, settings, CancellationToken.None);

        Assert.Equal(0, exitCode);

        var users = _fixture.Database.GetCollection<UserDocument>("users");
        var user = await users.Find(document => document.Username == "AlphaIntegration").FirstOrDefaultAsync();
        Assert.NotNull(user);
        Assert.Equal("alpha", user!.Bot);

        var roomObjects = _fixture.Database.GetCollection<BsonDocument>("rooms.objects");
        var spawnExists = await roomObjects.Find(doc => doc["room"] == roomName && doc["type"] == "spawn" && doc["user"] == user.Id)
                                           .AnyAsync();
        Assert.True(spawnExists);
    }

    [Fact]
    public async Task BotReloadCommand_ReplacesUserCodeBranch()
    {
        await _fixture.ResetAsync();
        var roomName = "W42N42";
        await GenerateRoomAsync(roomName);

        var botProvider = new InMemoryBotDefinitionProvider("beta");
        var service = CreateBotControlService(botProvider);
        var spawnCommand = new BotSpawnCommand(service);
        var username = "BetaIntegration";
        await spawnCommand.ExecuteAsync(null!, new BotSpawnCommand.Settings
        {
            BotName = "beta",
            RoomName = roomName,
            Username = username
        }, CancellationToken.None);

        var users = _fixture.Database.GetCollection<UserDocument>("users");
        var user = await users.Find(doc => doc.Username == username).FirstOrDefaultAsync();
        Assert.NotNull(user);

        var userCode = _fixture.Database.GetCollection<UserCodeDocument>("users.code");
        var before = await userCode.Find(doc => doc.UserId == user!.Id).FirstOrDefaultAsync();
        Assert.NotNull(before);

        var reloadCommand = new BotReloadCommand(service);
        var exitCode = await reloadCommand.ExecuteAsync(null!, new BotReloadCommand.Settings { BotName = "beta" }, CancellationToken.None);
        Assert.Equal(0, exitCode);

        var after = await userCode.Find(doc => doc.UserId == user.Id).ToListAsync();
        Assert.Single(after);
        Assert.NotEqual(before!.Branch, after[0].Branch);
    }

    [Fact]
    public async Task BotRemoveCommand_DeletesUserArtifacts()
    {
        await _fixture.ResetAsync();
        var roomName = "W43N43";
        await GenerateRoomAsync(roomName);

        var botProvider = new InMemoryBotDefinitionProvider("gamma");
        var service = CreateBotControlService(botProvider);
        var spawnCommand = new BotSpawnCommand(service);
        var username = "GammaIntegration";
        await spawnCommand.ExecuteAsync(null!, new BotSpawnCommand.Settings
        {
            BotName = "gamma",
            RoomName = roomName,
            Username = username
        }, CancellationToken.None);

        var removeCommand = new BotRemoveCommand(service);
        var exitCode = await removeCommand.ExecuteAsync(null!, new BotRemoveCommand.Settings { Username = username }, CancellationToken.None);
        Assert.Equal(0, exitCode);

        var users = _fixture.Database.GetCollection<UserDocument>("users");
        var user = await users.Find(doc => doc.Username == username).FirstOrDefaultAsync();
        Assert.Null(user);

        var roomObjects = _fixture.Database.GetCollection<BsonDocument>("rooms.objects");
        var spawnExists = await roomObjects.Find(doc => doc["room"] == roomName && doc["type"] == "spawn").AnyAsync();
        Assert.False(spawnExists);
    }

    private async Task GenerateRoomAsync(string roomName)
    {
        var mapCommand = new MapGenerateCommand(_fixture.MapControlService);
        await mapCommand.ExecuteAsync(null!, new MapGenerateCommand.Settings
        {
            RoomName = roomName,
            Overwrite = true
        }, CancellationToken.None);
    }

    private MongoBotControlService CreateBotControlService(IBotDefinitionProvider provider)
    {
        var databaseProvider = _fixture.DatabaseProvider;
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

    private sealed class InMemoryBotDefinitionProvider : IBotDefinitionProvider
    {
        private readonly Dictionary<string, BotDefinition> _definitions;

        public InMemoryBotDefinitionProvider(string botName)
        {
            _definitions = new Dictionary<string, BotDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                [botName] = new BotDefinition(botName,
                                              $"Definition for {botName}",
                                              new Dictionary<string, string>(StringComparer.Ordinal)
                                              {
                                                  ["main"] = "module.exports = {};"
                                              })
            };
        }

        public Task<IReadOnlyList<BotDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<BotDefinition>>(_definitions.Values.ToList());

        public Task<BotDefinition?> FindDefinitionAsync(string name, CancellationToken cancellationToken = default)
        {
            _definitions.TryGetValue(name, out var definition);
            return Task.FromResult<BotDefinition?>(definition);
        }
    }
}
