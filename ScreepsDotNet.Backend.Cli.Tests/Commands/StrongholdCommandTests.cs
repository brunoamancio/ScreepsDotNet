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

        var exitCode = await command.ExecuteAsync(null!, settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.NotNull(service.SpawnArguments);
        var (room, options) = service.SpawnArguments!.Value;
        Assert.Equal("W5N3", room);
        Assert.Equal("bunker3", options.TemplateName);
        Assert.Equal(20, options.X);
        Assert.Equal(22, options.Y);
        Assert.Equal("2", options.OwnerUserId);
        Assert.Equal(10, options.DeployDelayTicks);
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
        Assert.Contains("Both --x and --y", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StrongholdExpandCommand_ReturnsFailureWhenServiceReturnsFalse()
    {
        var service = new FakeStrongholdControlService { ExpandResult = false };
        var command = new StrongholdExpandCommand(service);
        var settings = new StrongholdExpandCommand.Settings { RoomName = "W5N3" };

        var exitCode = await command.ExecuteAsync(null!, settings, CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.Equal("W5N3", service.ExpandRoomName);
    }

    private sealed class FakeStrongholdControlService : IStrongholdControlService
    {
        public (string RoomName, StrongholdSpawnOptions Options)? SpawnArguments { get; private set; }
        public string? ExpandRoomName { get; private set; }
        public StrongholdSpawnResult SpawnResult { get; init; } = new("W5N3", "bunker3", "core-id");
        public bool ExpandResult { get; init; } = true;

        public Task<StrongholdSpawnResult> SpawnAsync(string roomName, StrongholdSpawnOptions options, CancellationToken cancellationToken = default)
        {
            SpawnArguments = (roomName, options);
            return Task.FromResult(SpawnResult);
        }

        public Task<bool> ExpandAsync(string roomName, CancellationToken cancellationToken = default)
        {
            ExpandRoomName = roomName;
            return Task.FromResult(ExpandResult);
        }
    }
}
