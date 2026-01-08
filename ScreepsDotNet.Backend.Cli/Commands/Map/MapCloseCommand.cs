namespace ScreepsDotNet.Backend.Cli.Commands.Map;

using global::System.ComponentModel;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class MapCloseCommand(IMapControlService mapControlService) : AsyncCommand<MapCloseCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--room <NAME>")]
        [Description("Room to close.")]
        public string RoomName { get; init; } = string.Empty;

        public override ValidationResult Validate()
        {
            return string.IsNullOrWhiteSpace(RoomName)
                ? ValidationResult.Error("Room name is required.")
                : ValidationResult.Success();
        }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        await mapControlService.CloseRoomAsync(settings.RoomName.Trim(), cancellationToken).ConfigureAwait(false);
        AnsiConsole.MarkupLine($"[yellow]Room {settings.RoomName} closed.[/]");
        return 0;
    }
}
