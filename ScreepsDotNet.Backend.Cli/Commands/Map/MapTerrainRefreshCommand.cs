namespace ScreepsDotNet.Backend.Cli.Commands.Map;

using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class MapTerrainRefreshCommand(IMapControlService mapControlService) : AsyncCommand<CommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, CommandSettings settings, CancellationToken cancellationToken)
    {
        await mapControlService.RefreshTerrainCacheAsync(cancellationToken).ConfigureAwait(false);
        AnsiConsole.MarkupLine("[green]Terrain cache refresh completed.[/]");
        return 0;
    }
}
