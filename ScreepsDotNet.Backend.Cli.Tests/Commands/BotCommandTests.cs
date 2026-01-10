namespace ScreepsDotNet.Backend.Cli.Tests.Commands;

using ScreepsDotNet.Backend.Cli.Commands.Bot;
using ScreepsDotNet.Backend.Core.Models.Bots;
using ScreepsDotNet.Backend.Core.Services;

public sealed class BotCommandTests
{
    [Fact]
    public async Task BotRemoveCommand_RemovesUser()
    {
        var service = new FakeBotControlService();
        var command = new BotRemoveCommand(service);
        var settings = new BotRemoveCommand.Settings
        {
            Username = "AlphaBot"
        };

        var exitCode = await command.ExecuteAsync(null!, settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal("AlphaBot", service.LastRemovedUsername);
    }

    private sealed class FakeBotControlService : IBotControlService
    {
        public string? LastRemovedUsername { get; private set; }
        public string? LastSpawnBotName { get; private set; }
        public int ReloadCount { get; private set; }

        public Task<BotSpawnResult> SpawnAsync(string botName, string roomName, string? shardName, BotSpawnOptions options, CancellationToken cancellationToken = default)
        {
            LastSpawnBotName = botName;
            return Task.FromResult(new BotSpawnResult("user-1", botName, roomName, shardName, options.SpawnX ?? 0, options.SpawnY ?? 0));
        }

        public Task<int> ReloadAsync(string botName, CancellationToken cancellationToken = default)
        {
            ReloadCount++;
            return Task.FromResult(1);
        }

        public Task<bool> RemoveAsync(string username, CancellationToken cancellationToken = default)
        {
            LastRemovedUsername = username;
            return Task.FromResult(true);
        }

    }
}
