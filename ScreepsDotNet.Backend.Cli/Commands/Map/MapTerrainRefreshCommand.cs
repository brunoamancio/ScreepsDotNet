namespace ScreepsDotNet.Backend.Cli.Commands.Map;

using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console.Cli;

internal sealed class MapTerrainRefreshCommand(IMapControlService mapControlService, ILogger<MapTerrainRefreshCommand>? logger = null, IHostApplicationLifetime? lifetime = null, ICommandOutputFormatter? outputFormatter = null) : CommandHandler<CommandSettings>(logger, lifetime, outputFormatter)
{
    protected override async Task<int> ExecuteCommandAsync(CommandContext context, CommandSettings settings, CancellationToken cancellationToken)
    {
        await mapControlService.RefreshTerrainCacheAsync(cancellationToken).ConfigureAwait(false);
        OutputFormatter.WriteMarkupLine("[green]Terrain cache refresh completed.[/]");
        return 0;
    }
}
