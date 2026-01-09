namespace ScreepsDotNet.Backend.Cli.Commands.Map;

using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class MapTerrainRefreshCommand(IMapControlService mapControlService, ILogger<MapTerrainRefreshCommand>? logger = null, IHostApplicationLifetime? lifetime = null) : CommandHandler<CommandSettings>(logger, lifetime)
{
    protected override async Task<int> ExecuteCommandAsync(CommandContext context, CommandSettings settings, CancellationToken cancellationToken)
    {
        await mapControlService.RefreshTerrainCacheAsync(cancellationToken).ConfigureAwait(false);
        AnsiConsole.MarkupLine("[green]Terrain cache refresh completed.[/]");
        return 0;
    }
}
