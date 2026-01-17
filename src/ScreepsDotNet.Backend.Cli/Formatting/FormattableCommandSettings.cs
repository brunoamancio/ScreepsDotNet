namespace ScreepsDotNet.Backend.Cli.Formatting;

using System;
using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

internal abstract class FormattableCommandSettings : CommandSettings, IFormattableCommandSettings
{
    internal const string FormatEnvironmentVariableName = "SCREEPSCLI_FORMAT";

    [CommandOption("--format <FORMAT>")]
    [Description("Output format (table|markdown|json).")]
    public string? Format { get; init; }

    public OutputFormat? PreferredOutputFormat
        => OutputFormatParser.TryParse(ResolveFormat(), out var parsed) ? parsed : null;

    public override ValidationResult Validate()
    {
        var format = ResolveFormat();
        if (!string.IsNullOrWhiteSpace(format) && !OutputFormatParser.TryParse(format, out _))
            return ValidationResult.Error("Invalid format. Supported values: table, markdown, json.");

        return ValidationResult.Success();
    }

    private string? ResolveFormat()
    {
        if (!string.IsNullOrWhiteSpace(Format))
            return Format;

        var envValue = Environment.GetEnvironmentVariable(FormatEnvironmentVariableName);
        var result = string.IsNullOrWhiteSpace(envValue) ? null : envValue;
        return result;
    }
}
