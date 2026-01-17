namespace ScreepsDotNet.Backend.Cli.Formatting;

internal static class OutputFormatParser
{
    public static bool TryParse(string? value, out OutputFormat format)
    {
        if (value is null) {
            format = OutputFormat.Table;
            return false;
        }

        switch (value.Trim().ToLowerInvariant()) {
            case "table":
                format = OutputFormat.Table;
                return true;
            case "markdown":
            case "md":
                format = OutputFormat.Markdown;
                return true;
            case "json":
                format = OutputFormat.Json;
                return true;
            default:
                format = OutputFormat.Table;
                return false;
        }
    }
}
