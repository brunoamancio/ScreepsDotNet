namespace ScreepsDotNet.Backend.Cli.Tests.Commands;

using System.Collections.Generic;
using ScreepsDotNet.Backend.Cli.Commands.World;
using ScreepsDotNet.Backend.Cli.Formatting;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using Spectre.Console;

public sealed class WorldCommandTests
{
    [Fact]
    public async Task WorldStatsCommand_ForwardsRoomsAndStat()
    {
        var repository = new FakeWorldStatsRepository();
        var command = new WorldStatsCommand(repository, null, null, new TestFormatter());
        var settings = new WorldStatsCommand.Settings
        {
            Rooms = ["W1N1", "shard2/W2N2"],
            StatName = "owners3",
            OutputJson = true
        };

        var exitCode = await command.ExecuteAsync(null!, settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.NotNull(repository.Request);
        Assert.Equal("owners3", repository.Request!.StatName);
        Assert.Equal(2, repository.Request.Rooms.Count);
        Assert.Contains(repository.Request.Rooms, reference => reference.RoomName == "W1N1" && reference.ShardName is null);
        Assert.Contains(repository.Request.Rooms, reference => reference.RoomName == "W2N2" && reference.ShardName == "shard2");
    }

    [Fact]
    public async Task WorldOverviewCommand_PassesNormalizedReference()
    {
        var repository = new FakeRoomOverviewRepository();
        var command = new WorldOverviewCommand(repository, null, null, new TestFormatter());
        var settings = new WorldOverviewCommand.Settings
        {
            RoomName = "shard3/W7S2",
            OutputJson = true
        };

        var exitCode = await command.ExecuteAsync(null!, settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.NotNull(repository.Reference);
        Assert.Equal("W7S2", repository.Reference!.RoomName);
        Assert.Equal("shard3", repository.Reference.ShardName);
    }

    private sealed class FakeWorldStatsRepository : IWorldStatsRepository
    {
        public MapStatsRequest? Request { get; private set; }

        public Task<MapStatsResult> GetMapStatsAsync(MapStatsRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            var room = new MapStatsRoom("W1N1", "normal", false, false, null, null, null, false, null);
            var stats = new Dictionary<string, MapStatsRoom>(StringComparer.OrdinalIgnoreCase) { ["W1N1"] = room };
            var result = new MapStatsResult(1234, stats, new Dictionary<string, object?>(), new Dictionary<string, MapStatsUser>());
            return Task.FromResult(result);
        }
    }

    private sealed class FakeRoomOverviewRepository : IRoomOverviewRepository
    {
        public RoomReference? Reference { get; private set; }

        public Task<RoomOverview?> GetRoomOverviewAsync(RoomReference room, CancellationToken cancellationToken = default)
        {
            Reference = room;
            return Task.FromResult<RoomOverview?>(new RoomOverview(room, null));
        }
    }
    private sealed class TestFormatter : ICommandOutputFormatter
    {
        public List<string> JsonPayloads { get; } = [];
        public OutputFormat PreferredFormat { get; private set; } = OutputFormat.Table;

        public void SetPreferredFormat(OutputFormat format)
            => PreferredFormat = format;

        public void WriteJson<T>(T payload)
            => JsonPayloads.Add(payload?.ToString() ?? string.Empty);

        public void WriteTable(Table table)
        {
        }

        public void WriteTabularData(string? title, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
        {
        }

        public void WriteKeyValueTable(IEnumerable<(string Key, string Value)> rows, string? title = null)
        {
        }

        public void WriteMarkdownTable(string? title, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
        {
        }

        public void WriteLine(string message)
        {
        }

        public void WriteMarkupLine(string markup, params object[] args)
        {
        }
    }
}
