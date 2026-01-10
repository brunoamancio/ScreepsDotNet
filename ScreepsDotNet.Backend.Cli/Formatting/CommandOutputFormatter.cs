namespace ScreepsDotNet.Backend.Cli.Formatting;

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Spectre.Console;

internal interface ICommandOutputFormatter
{
    void WriteJson<T>(T payload);

    void WriteTable(Table table);

    void WriteKeyValueTable(IEnumerable<(string Key, string Value)> rows, string? title = null);

    void WriteMarkdownTable(string? title, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows);

    void WriteLine(string message);

    void WriteMarkupLine(string markup, params object[] args);
}

internal sealed class CommandOutputFormatter : ICommandOutputFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public void WriteJson<T>(T payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        AnsiConsole.WriteLine(json);
    }

    public void WriteTable(Table table)
        => AnsiConsole.Write(table);

    public void WriteKeyValueTable(IEnumerable<(string Key, string Value)> rows, string? title = null)
    {
        var table = new Table();
        if (!string.IsNullOrWhiteSpace(title))
            table.Title = new TableTitle(title);

        table.AddColumn("Key");
        table.AddColumn("Value");

        foreach (var (key, value) in rows)
            table.AddRow(key, value);

        AnsiConsole.Write(table);
    }

    public void WriteMarkdownTable(string? title, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(title))
            builder.AppendLine($"# {title}");

        builder.Append("| ");
        builder.Append(string.Join(" | ", headers));
        builder.AppendLine(" |");
        builder.Append("| ");
        builder.Append(string.Join(" | ", headers.Select(_ => "---")));
        builder.AppendLine(" |");

        foreach (var row in rows) {
            builder.Append("| ");
            builder.Append(string.Join(" | ", row));
            builder.AppendLine(" |");
        }

        AnsiConsole.WriteLine(builder.ToString());
    }

    public void WriteLine(string message)
        => AnsiConsole.WriteLine(message);

    public void WriteMarkupLine(string markup, params object[] args)
        => AnsiConsole.MarkupLine(markup, args);
}
