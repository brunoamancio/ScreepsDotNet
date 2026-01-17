namespace ScreepsDotNet.Backend.Cli.Tests.Commands;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Cli.Commands.Storage;
using ScreepsDotNet.Backend.Cli.Formatting;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Storage.MongoRedis.Options;
using ScreepsDotNet.Storage.MongoRedis.Seeding;
using Spectre.Console;

public sealed class StorageCommandTests
{
    [Fact]
    public async Task StorageReseedCommand_RequiresConfirmationToken()
    {
        var command = new StorageReseedCommand(new FakeSeedDataService(),
                                               Options.Create(new MongoRedisStorageOptions
                                               {
                                                   MongoConnectionString = "mongodb://localhost:27017",
                                                   MongoDatabase = SeedDataDefaults.Database.Name
                                               }),
                                               NullLogger<StorageReseedCommand>.Instance,
                                               null,
                                               new TestFormatter());

        var settings = new StorageReseedCommand.Settings
        {
            Force = true,
            Confirm = null,
            Format = "table"
        };

        var exitCode = await command.ExecuteAsync(null!, settings, TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
    }

    private sealed class FakeSeedDataService : ISeedDataService
    {
        public Task ReseedAsync(string mongoConnectionString, string databaseName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ReseedAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
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
