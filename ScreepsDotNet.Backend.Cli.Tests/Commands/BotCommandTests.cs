namespace ScreepsDotNet.Backend.Cli.Tests.Commands;

using ScreepsDotNet.Backend.Cli.Commands.Bot;
using ScreepsDotNet.Backend.Core.Models.Bots;
using ScreepsDotNet.Backend.Core.Services;

public sealed class BotCommandTests
{
    [Fact]
    public async Task BotSpawnCommand_ForwardsOptionsToService()
    {
        var service = new FakeBotControlService();
        var command = new BotSpawnCommand(service);
        var settings = new BotSpawnCommand.Settings
        {
            BotName = "alpha",
            RoomName = "W1N1",
            Shard = "shard2",
            Username = "OmegaBot",
            Cpu = 150,
            GlobalControlLevel = 3,
            SpawnX = 10,
            SpawnY = 12
        };

        var exitCode = await command.ExecuteAsync(null!, settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.NotNull(service.SpawnArguments);
        var (bot, room, shard, options) = service.SpawnArguments!.Value;
        Assert.Equal("alpha", bot);
        Assert.Equal("W1N1", room);
        Assert.Equal("shard2", shard);
        Assert.Equal("OmegaBot", options.Username);
        Assert.Equal(150, options.Cpu);
        Assert.Equal(3, options.GlobalControlLevel);
        Assert.Equal(10, options.SpawnX);
        Assert.Equal(12, options.SpawnY);
    }

    [Fact]
    public void BotSpawnSettings_MissingCoordinatePair_FailsValidation()
    {
        var settings = new BotSpawnCommand.Settings
        {
            BotName = "alpha",
            RoomName = "W1N1",
            SpawnX = 5
        };

        var result = settings.Validate();

        Assert.False(result.Successful);
        Assert.Contains("must be set together", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BotRemoveCommand_ReturnsErrorWhenServiceReturnsFalse()
    {
        var service = new FakeBotControlService { RemoveResult = false };
        var command = new BotRemoveCommand(service);
        var settings = new BotRemoveCommand.Settings { Username = "NobodyBot" };

        var exitCode = await command.ExecuteAsync(null!, settings, CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.Equal("NobodyBot", service.RemovedUsername);
    }

    private sealed class FakeBotControlService : IBotControlService
    {
        public (string BotName, string RoomName, string? ShardName, BotSpawnOptions Options)? SpawnArguments { get; private set; }
        public string? ReloadedBotName { get; private set; }
        public string? RemovedUsername { get; private set; }
        public BotSpawnResult SpawnResult { get; init; } = new("user-1", "OmegaBot", "W1N1", "shard2", 10, 12);
        public int ReloadResult { get; init; }
        public bool RemoveResult { get; init; } = true;

        public Task<BotSpawnResult> SpawnAsync(string botName, string roomName, string? shardName, BotSpawnOptions options, CancellationToken cancellationToken = default)
        {
            SpawnArguments = (botName, roomName, shardName, options);
            return Task.FromResult(SpawnResult);
        }

        public Task<int> ReloadAsync(string botName, CancellationToken cancellationToken = default)
        {
            ReloadedBotName = botName;
            return Task.FromResult(ReloadResult);
        }

        public Task<bool> RemoveAsync(string username, CancellationToken cancellationToken = default)
        {
            RemovedUsername = username;
            return Task.FromResult(RemoveResult);
        }
    }
}
