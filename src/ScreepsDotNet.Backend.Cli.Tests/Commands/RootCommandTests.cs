namespace ScreepsDotNet.Backend.Cli.Tests.Commands;

using ScreepsDotNet.Backend.Cli.Commands;
using ScreepsDotNet.Backend.Cli.Formatting;
using Spectre.Console;

public sealed class RootCommandTests
{
    [Fact]
    public async Task RootCommand_HonorsFormatSetting()
    {
        var formatter = new TestFormatter();
        var command = new RootCommand(null, null, formatter);
        var settings = new RootCommandSettings
        {
            Format = "markdown"
        };

        var exitCode = await command.ExecuteAsync(null!, settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.True(formatter.MarkdownRequested);
    }

    private sealed class TestFormatter : ICommandOutputFormatter
    {
        public OutputFormat PreferredFormat { get; private set; } = OutputFormat.Table;
        public bool MarkdownRequested { get; private set; }

        public void SetPreferredFormat(OutputFormat format)
        {
            PreferredFormat = format;
            if (format == OutputFormat.Markdown)
                MarkdownRequested = true;
        }

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
