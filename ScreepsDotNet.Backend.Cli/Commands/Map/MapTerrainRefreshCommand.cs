namespace ScreepsDotNet.Backend.Cli.Commands.Map;

using ScreepsDotNet.Backend.Cli.Formatting;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console.Cli;

internal sealed class MapTerrainRefreshCommand(IMapControlService mapControlService, ILogger<MapTerrainRefreshCommand>? logger = null, IHostApplicationLifetime? lifetime = null, ICommandOutputFormatter? outputFormatter = null) : CommandHandler<MapTerrainRefreshCommand.Settings>(logger, lifetime, outputFormatter)
{
    public sealed class Settings : FormattableCommandSettings
    {
        [CommandOption("--json")]
        public bool OutputJson { get; init; }
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        await mapControlService.RefreshTerrainCacheAsync(cancellationToken).ConfigureAwait(false);

        if (settings.OutputJson) {
            OutputFormatter.WriteJson(new { refreshed = true });
            return 0;
        }

        OutputFormatter.WriteKeyValueTable([("Terrain Cache", "refreshed")], "Terrain refresh");
        return 0;
    }
}
