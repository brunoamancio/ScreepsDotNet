namespace ScreepsDotNet.Backend.Cli.Tests.Integration;

using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Cli.Commands.User;
using ScreepsDotNet.Backend.Cli.Formatting;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Storage.MongoRedis.Repositories;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;
using Spectre.Console;

public sealed class UserCommandsIntegrationTests(MongoMapIntegrationFixture fixture) : IClassFixture<MongoMapIntegrationFixture>
{
    [Fact]
    public async Task UserConsoleExecCommand_EnqueuesExpression()
    {
        await fixture.ResetAsync();
        var repository = new MongoUserConsoleRepository(fixture.DatabaseProvider);
        var command = new UserConsoleExecCommand(repository, NullLogger<UserConsoleExecCommand>.Instance);
        var settings = new UserConsoleExecCommand.Settings
        {
            UserId = SeedDataDefaults.User.Id,
            Expression = "console.log('hi from cli');",
            Hidden = true,
            OutputJson = true
        };

        var exitCode = await command.ExecuteAsync(null!, settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
        var entries = fixture.GetCollection<UserConsoleEntryDocument>(fixture.DatabaseProvider.Settings.UserConsoleCollection);
        var stored = await entries.Find(doc => doc.UserId == SeedDataDefaults.User.Id && doc.Expression.Contains("hi from cli")).FirstOrDefaultAsync();
        Assert.NotNull(stored);
        Assert.True(stored!.Hidden);
    }

    [Fact]
    public async Task UserMemorySetCommand_UpdatesRootMemory()
    {
        await fixture.ResetAsync();
        var repository = new MongoUserMemoryRepository(fixture.DatabaseProvider);
        var command = new UserMemorySetCommand(repository, NullLogger<UserMemorySetCommand>.Instance);
        var settings = new UserMemorySetCommand.Settings
        {
            UserId = SeedDataDefaults.User.Id,
            JsonValue = "{\"theme\":\"dark\"}",
            OutputJson = true
        };

        var exitCode = await command.ExecuteAsync(null!, settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
        var collection = fixture.GetCollection<UserMemoryDocument>(fixture.DatabaseProvider.Settings.UserMemoryCollection);
        var document = await collection.Find(doc => doc.UserId == SeedDataDefaults.User.Id).FirstOrDefaultAsync();
        Assert.NotNull(document);
        Assert.True(document!.Memory.TryGetValue("theme", out var value));
        Assert.Equal("dark", value);
    }

    [Fact]
    public async Task UserMemorySetCommand_UpdatesSegment()
    {
        await fixture.ResetAsync();
        var repository = new MongoUserMemoryRepository(fixture.DatabaseProvider);
        var command = new UserMemorySetCommand(repository, NullLogger<UserMemorySetCommand>.Instance);
        var settings = new UserMemorySetCommand.Settings
        {
            UserId = SeedDataDefaults.User.Id,
            Segment = 7,
            SegmentData = "segment-data",
            OutputJson = true
        };

        var exitCode = await command.ExecuteAsync(null!, settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
        var collection = fixture.GetCollection<UserMemoryDocument>(fixture.DatabaseProvider.Settings.UserMemoryCollection);
        var document = await collection.Find(doc => doc.UserId == SeedDataDefaults.User.Id).FirstOrDefaultAsync();
        Assert.NotNull(document);
        Assert.True(document!.Segments.TryGetValue("7", out var data));
        Assert.Equal("segment-data", data);
    }

    [Fact]
    public async Task UserMemoryGetCommand_ReadsSegment()
    {
        await fixture.ResetAsync();
        var repository = new MongoUserMemoryRepository(fixture.DatabaseProvider);
        await repository.SetMemorySegmentAsync(SeedDataDefaults.User.Id, 5, "seeded", CancellationToken.None);

        var command = new UserMemoryGetCommand(repository, NullLogger<UserMemoryGetCommand>.Instance, null, new TestFormatter());
        var settings = new UserMemoryGetCommand.Settings
        {
            UserId = SeedDataDefaults.User.Id,
            Segment = 5,
            OutputJson = true
        };

        var exitCode = await command.ExecuteAsync(null!, settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
    }

    private sealed class TestFormatter : ICommandOutputFormatter
    {
        public OutputFormat PreferredFormat { get; private set; } = OutputFormat.Table;

        public void SetPreferredFormat(OutputFormat format)
            => PreferredFormat = format;

        public void WriteJson<T>(T payload)
        {
        }

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
