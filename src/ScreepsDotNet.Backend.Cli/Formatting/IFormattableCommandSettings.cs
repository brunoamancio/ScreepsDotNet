namespace ScreepsDotNet.Backend.Cli.Formatting;

internal interface IFormattableCommandSettings
{
    OutputFormat? PreferredOutputFormat { get; }
}
