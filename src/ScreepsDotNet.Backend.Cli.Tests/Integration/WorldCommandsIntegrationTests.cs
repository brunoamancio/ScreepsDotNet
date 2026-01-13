namespace ScreepsDotNet.Backend.Cli.Tests.Integration;

using System;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using ScreepsDotNet.Backend.Cli.Commands.World;
using ScreepsDotNet.Backend.Cli.Formatting;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Storage.MongoRedis.Repositories;
using Spectre.Console;

public sealed class WorldCommandsIntegrationTests(MongoMapIntegrationFixture fixture) : IClassFixture<MongoMapIntegrationFixture>
{
    [Fact]
    public async Task WorldDumpCommand_ReturnsRoomTerrain()
    {
        await fixture.ResetAsync();
        var formatter = new CapturingFormatter();
        var repository = new MongoRoomTerrainRepository(fixture.DatabaseProvider);
        var command = new WorldDumpCommand(repository, NullLogger<WorldDumpCommand>.Instance, null, formatter);
        var settings = new WorldDumpCommand.Settings
        {
            Rooms = [SeedDataDefaults.World.StartRoom],
            DecodeTiles = false,
            OutputJson = true
        };

        var exitCode = await command.ExecuteAsync(null!, settings, TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains(SeedDataDefaults.World.StartRoom, formatter.LastJson ?? string.Empty);
    }

    [Fact]
    public async Task WorldOverviewCommand_ReturnsOwner()
    {
        await fixture.ResetAsync();
        var formatter = new CapturingFormatter();
        var overviewRepository = new MongoRoomOverviewRepository(fixture.DatabaseProvider);
        var command = new WorldOverviewCommand(overviewRepository, NullLogger<WorldOverviewCommand>.Instance, null, formatter);
        var settings = new WorldOverviewCommand.Settings
        {
            RoomName = SeedDataDefaults.World.StartRoom,
            OutputJson = true
        };

        var exitCode = await command.ExecuteAsync(null!, settings, TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains(SeedDataDefaults.World.StartRoom, formatter.LastJson ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WorldStatsCommand_ReturnsStats()
    {
        await fixture.ResetAsync();
        var formatter = new CapturingFormatter();
        var metadataRepository = new MongoWorldMetadataRepository(fixture.DatabaseProvider);
        var statsRepository = new MongoWorldStatsRepository(fixture.DatabaseProvider, metadataRepository);
        var command = new WorldStatsCommand(statsRepository, NullLogger<WorldStatsCommand>.Instance, null, formatter);
        var settings = new WorldStatsCommand.Settings
        {
            Rooms = [SeedDataDefaults.World.StartRoom],
            StatName = "owners1",
            OutputJson = true
        };

        var exitCode = await command.ExecuteAsync(null!, settings, TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains(SeedDataDefaults.World.StartRoom, formatter.LastJson ?? string.Empty);
    }

    private sealed class CapturingFormatter : ICommandOutputFormatter
    {
        public string? LastJson { get; private set; }
        public OutputFormat PreferredFormat { get; private set; } = OutputFormat.Table;

        public void SetPreferredFormat(OutputFormat format)
            => PreferredFormat = format;

        public void WriteJson<T>(T payload)
            => LastJson = JsonSerializer.Serialize(payload);

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
