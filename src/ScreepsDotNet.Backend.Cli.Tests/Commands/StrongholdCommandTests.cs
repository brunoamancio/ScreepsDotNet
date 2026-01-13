namespace ScreepsDotNet.Backend.Cli.Tests.Commands;

using ScreepsDotNet.Backend.Cli.Commands.Stronghold;
using ScreepsDotNet.Backend.Core.Models.Strongholds;
using ScreepsDotNet.Backend.Core.Services;

public sealed class StrongholdCommandTests
{
    [Fact]
    public async Task StrongholdSpawnCommand_ForwardsTemplateAndCoordinates()
    {
        var service = new FakeStrongholdControlService();
        var command = new StrongholdSpawnCommand(service);
        var settings = new StrongholdSpawnCommand.Settings
        {
            RoomName = "W5N3",
            TemplateName = "bunker3",
            X = 20,
            Y = 22,
            OwnerUserId = "2",
            DeployDelayTicks = 10
        };

        var exitCode = await command.ExecuteAsync(null!, settings, TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.NotNull(service.SpawnArguments);
        var (room, shard, options) = service.SpawnArguments!.Value;
        Assert.Equal("W5N3", room);
        Assert.Null(shard);
        Assert.Equal("bunker3", options.TemplateName);
        Assert.Equal(20, options.X);
        Assert.Equal(22, options.Y);
        Assert.Equal("2", options.OwnerUserId);
        Assert.Equal(10, options.DeployDelayTicks);
    }

    [Fact]
    public async Task StrongholdSpawnCommand_AcceptsShardOverride()
    {
        var service = new FakeStrongholdControlService();
        var command = new StrongholdSpawnCommand(service);
        var settings = new StrongholdSpawnCommand.Settings
        {
            RoomName = "W6N4",
            Shard = "shard2"
        };

        var exitCode = await command.ExecuteAsync(null!, settings, TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Equal("shard2", service.SpawnArguments!.Value.ShardName);
    }

    [Fact]
    public void StrongholdSpawnSettings_InvalidCoordinatePairFails()
    {
        var settings = new StrongholdSpawnCommand.Settings
        {
            RoomName = "W5N3",
            X = 10
        };

        var result = settings.Validate();

        Assert.False(result.Successful);
        Assert.Contains("Both -x/--pos-x and -y/--pos-y", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StrongholdExpandCommand_ReturnsFailureWhenServiceReturnsFalse()
    {
        var service = new FakeStrongholdControlService { ExpandResult = false };
        var command = new StrongholdExpandCommand(service);
        var settings = new StrongholdExpandCommand.Settings { RoomName = "W5N3", Shard = "shard1" };

        var exitCode = await command.ExecuteAsync(null!, settings, TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Equal(("W5N3", "shard1"), service.ExpandArguments);
    }

    private sealed class FakeStrongholdControlService : IStrongholdControlService
    {
        public (string RoomName, string? ShardName, StrongholdSpawnOptions Options)? SpawnArguments { get; private set; }
        public (string RoomName, string? ShardName)? ExpandArguments { get; private set; }
        public StrongholdSpawnResult SpawnResult { get; init; } = new("W5N3", null, "bunker3", "core-id");
        public bool ExpandResult { get; init; } = true;

        public Task<StrongholdSpawnResult> SpawnAsync(string roomName, string? shardName, StrongholdSpawnOptions options, CancellationToken cancellationToken = default)
        {
            SpawnArguments = (roomName, shardName, options);
            return Task.FromResult(SpawnResult);
        }

        public Task<bool> ExpandAsync(string roomName, string? shardName, CancellationToken cancellationToken = default)
        {
            ExpandArguments = (roomName, shardName);
            return Task.FromResult(ExpandResult);
        }
    }
}
