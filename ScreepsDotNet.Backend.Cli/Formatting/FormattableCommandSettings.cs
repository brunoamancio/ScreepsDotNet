namespace ScreepsDotNet.Backend.Cli.Formatting;

using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

internal abstract class FormattableCommandSettings : CommandSettings, IFormattableCommandSettings
{
    [CommandOption("--format <FORMAT>")]
    [Description("Output format (table|markdown|json).")]
    public string? Format { get; init; }

    public OutputFormat? PreferredOutputFormat
        => OutputFormatParser.TryParse(Format, out var parsed) ? parsed : null;

    public override ValidationResult Validate()
    {
        if (!string.IsNullOrWhiteSpace(Format) && !OutputFormatParser.TryParse(Format, out _))
            return ValidationResult.Error("Invalid format. Supported values: table, markdown, json.");

        return ValidationResult.Success();
    }
}
