using Spectre.Console.Cli;

namespace ScreepsDotNet.Backend.Cli.Commands;

internal sealed class RootCommand(ILogger<RootCommand>? logger = null, IHostApplicationLifetime? lifetime = null, ICommandOutputFormatter? outputFormatter = null) : CommandHandler<RootCommandSettings>(logger, lifetime, outputFormatter)
{
    protected override Task<int> ExecuteCommandAsync(CommandContext context, RootCommandSettings settings, CancellationToken cancellationToken)
    {
        LogConfigurationSummary(settings);
        OutputFormatter.WriteMarkupLine("[bold]No command provided.[/] Use [green]--help[/] to see available commands.");
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
}
