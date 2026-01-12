namespace ScreepsDotNet.Backend.Cli.Formatting;

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Spectre.Console;

internal interface ICommandOutputFormatter
{
    OutputFormat PreferredFormat { get; }

    void SetPreferredFormat(OutputFormat format);

    void WriteJson<T>(T payload);

    void WriteTable(Table table);

    void WriteTabularData(string? title, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows);

    void WriteKeyValueTable(IEnumerable<(string Key, string Value)> rows, string? title = null);

    void WriteMarkdownTable(string? title, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows);

    void WriteLine(string message);

    void WriteMarkupLine(string markup, params object[] args);
}

internal sealed class CommandOutputFormatter : ICommandOutputFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly string[] KeyValueHeaders = ["Key", "Value"];
    private OutputFormat _preferredFormat = OutputFormat.Table;

    public OutputFormat PreferredFormat => _preferredFormat;

    public void SetPreferredFormat(OutputFormat format)
        => _preferredFormat = format;

    public void WriteJson<T>(T payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        AnsiConsole.WriteLine(json);
    }

    public void WriteTable(Table table)
        => AnsiConsole.Write(table);

    public void WriteTabularData(string? title, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        switch (_preferredFormat)
        {
            case OutputFormat.Json:
                var jsonRows = rows.Select(row => headers.Zip(row, (header, value) => (header, value))
                                                         .ToDictionary(pair => pair.header, pair => pair.value))
                                   .ToList();
                WriteJson(jsonRows);
                return;
            case OutputFormat.Markdown:
                WriteMarkdownTable(title, headers, rows);
                return;
            default:
                var table = new Table();
                if (!string.IsNullOrWhiteSpace(title))
                    table.Title = new TableTitle(title);

                foreach (var header in headers)
                    table.AddColumn(header);

                foreach (var row in rows)
                    table.AddRow(row.ToArray());

                AnsiConsole.Write(table);
                return;
        }
    }

    public void WriteKeyValueTable(IEnumerable<(string Key, string Value)> rows, string? title = null)
    {
        var rowList = rows.Select(tuple => (tuple.Key ?? string.Empty, tuple.Value ?? string.Empty)).ToList();

        WriteTabularData(title,
                         KeyValueHeaders,
                         rowList.Select(row => (IReadOnlyList<string>)[row.Item1, row.Item2]));
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
