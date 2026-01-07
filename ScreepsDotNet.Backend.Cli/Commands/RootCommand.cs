using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ScreepsDotNet.Backend.Cli.Commands;

internal sealed class RootCommand(ILogger<RootCommand> logger) : AsyncCommand<RootCommandSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, RootCommandSettings settings, CancellationToken cancellationToken)
    {
        LogConfigurationSummary(settings);
        AnsiConsole.MarkupLine("[bold]No command provided.[/] Use [green]--help[/] to see available commands (coming in Phase 2).");
        return Task.FromResult(0);
    }

    private void LogConfigurationSummary(RootCommandSettings settings)
    {
        logger.LogInformation("Storage backend: {Backend}", settings.StorageBackend);

        if (!string.IsNullOrWhiteSpace(settings.ConnectionString))
            logger.LogInformation("Custom Mongo connection string supplied.");

        if (!string.IsNullOrWhiteSpace(settings.CliHost) || settings.CliPort.HasValue)
            logger.LogInformation("Legacy CLI listener requested on {Host}:{Port}", settings.CliHost ?? "default", settings.CliPort ?? -1);

        if (!string.IsNullOrWhiteSpace(settings.Host) || settings.Port.HasValue)
            logger.LogInformation("HTTP host override requested: {Host}:{Port}", settings.Host ?? "default", settings.Port ?? -1);

        if (!string.IsNullOrWhiteSpace(settings.Password))
            logger.LogInformation("Server password supplied.");

        if (!string.IsNullOrWhiteSpace(settings.SteamApiKey))
            logger.LogInformation("Steam API key provided.");

        if (settings.RunnerCount.HasValue || settings.ProcessorCount.HasValue)
            logger.LogInformation("Worker counts => runners: {Runners}, processors: {Processors}", settings.RunnerCount, settings.ProcessorCount);
    }
}
