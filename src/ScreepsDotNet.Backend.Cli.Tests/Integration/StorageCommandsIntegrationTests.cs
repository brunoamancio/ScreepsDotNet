namespace ScreepsDotNet.Backend.Cli.Tests.Integration;

using Microsoft.Extensions.Logging.Abstractions;
using ScreepsDotNet.Backend.Cli.Commands.Storage;
using ScreepsDotNet.Backend.Cli.Formatting;
using ScreepsDotNet.Storage.MongoRedis.Adapters;
using Spectre.Console;

public sealed class StorageCommandsIntegrationTests(SystemCommandsIntegrationFixture fixture) : IClassFixture<SystemCommandsIntegrationFixture>
{
    [Fact]
    public async Task StorageStatusCommand_ReportsConnected()
    {
        await fixture.ResetStateAsync();
        var adapter = new MongoRedisStorageAdapter(fixture.DatabaseProvider, fixture.RedisProvider, NullLogger<MongoRedisStorageAdapter>.Instance);
        var command = new StorageStatusCommand(adapter, NullLogger<StorageStatusCommand>.Instance, null, new TestFormatter());

        var exitCode = await command.ExecuteAsync(null!, new StorageStatusCommand.Settings { OutputJson = true }, CancellationToken.None);

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
