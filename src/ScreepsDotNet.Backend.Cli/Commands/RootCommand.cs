using Spectre.Console.Cli;

namespace ScreepsDotNet.Backend.Cli.Commands;

internal sealed class RootCommand(ILogger<RootCommand>? logger = null, IHostApplicationLifetime? lifetime = null, ICommandOutputFormatter? outputFormatter = null)
    : CommandHandler<RootCommandSettings>(logger, lifetime, outputFormatter)
{
    protected override Task<int> ExecuteCommandAsync(CommandContext context, RootCommandSettings settings, CancellationToken cancellationToken)
    {
        LogConfigurationSummary(settings);
        OutputFormatter.WriteKeyValueTable(BuildSummaryRows(settings), "Global options");
        OutputFormatter.WriteKeyValueTable([
                                               ("Status", "No command provided"),
                                               ("Hint", "Run screeps-cli --help or screeps-cli <command> --help")
                                           ],
                                           "Usage");
        return Task.FromResult(0);
    }

    private void LogConfigurationSummary(RootCommandSettings settings)
    {
        Logger.LogInformation("Storage backend: {Backend}", settings.StorageBackend);

        if (!string.IsNullOrWhiteSpace(settings.ConnectionString))
            Logger.LogInformation("Custom Mongo connection string supplied.");

        if (!string.IsNullOrWhiteSpace(settings.CliHost) || settings.CliPort.HasValue)
            Logger.LogInformation("Legacy CLI listener requested on {Host}:{Port}", settings.CliHost ?? "default", settings.CliPort ?? -1);

        if (!string.IsNullOrWhiteSpace(settings.Host) || settings.Port.HasValue)
            Logger.LogInformation("HTTP host override requested: {Host}:{Port}", settings.Host ?? "default", settings.Port ?? -1);

        if (!string.IsNullOrWhiteSpace(settings.Password))
            Logger.LogInformation("Server password supplied.");

        if (!string.IsNullOrWhiteSpace(settings.SteamApiKey))
            Logger.LogInformation("Steam API key provided.");

        if (settings.RunnerCount.HasValue || settings.ProcessorCount.HasValue)
            Logger.LogInformation("Worker counts => runners: {Runners}, processors: {Processors}", settings.RunnerCount, settings.ProcessorCount);
    }

    private static IEnumerable<(string Key, string Value)> BuildSummaryRows(RootCommandSettings settings)
    {
        yield return ("Storage backend", settings.StorageBackend);
        yield return ("Mongo connection", string.IsNullOrWhiteSpace(settings.ConnectionString) ? "(default)" : "(custom override)");
        yield return ("CLI host", settings.CliHost ?? "(default)");
        yield return ("CLI port", settings.CliPort?.ToString() ?? "(default)");
        yield return ("HTTP host", settings.Host ?? "(default)");
        yield return ("HTTP port", settings.Port?.ToString() ?? "(default)");
        yield return ("Password configured", string.IsNullOrWhiteSpace(settings.Password) ? "no" : "yes");
        yield return ("Steam API key", string.IsNullOrWhiteSpace(settings.SteamApiKey) ? "not set" : "provided");
        yield return ("Runner count", settings.RunnerCount?.ToString() ?? "(default)");
        yield return ("Processor count", settings.ProcessorCount?.ToString() ?? "(default)");
        yield return ("Asset dir", settings.AssetDirectory ?? "(default)");
        yield return ("Log dir", settings.LogDirectory ?? "(default)");
        yield return ("Mod file", settings.ModFile ?? "(default)");
    }
}
