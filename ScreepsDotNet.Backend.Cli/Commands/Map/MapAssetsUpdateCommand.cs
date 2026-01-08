namespace ScreepsDotNet.Backend.Cli.Commands.Map;

using global::System.ComponentModel;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class MapAssetsUpdateCommand(IMapControlService mapControlService) : AsyncCommand<MapAssetsUpdateCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--room <NAME>")]
        [Description("Room whose assets should be regenerated.")]
        public string RoomName { get; init; } = string.Empty;

        [CommandOption("--full")]
        [Description("Force full regeneration instead of incremental update.")]
        public bool Full { get; init; }

        public override ValidationResult Validate()
        {
            return string.IsNullOrWhiteSpace(RoomName)
                ? ValidationResult.Error("Room name is required.")
                : ValidationResult.Success();
        }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        await mapControlService.UpdateRoomAssetsAsync(settings.RoomName.Trim(), settings.Full, cancellationToken).ConfigureAwait(false);
        AnsiConsole.MarkupLine($"[blue]Queued asset refresh for {settings.RoomName} (full={settings.Full}).[/]");
        return 0;
    }
}
