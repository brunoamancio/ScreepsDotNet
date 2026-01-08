namespace ScreepsDotNet.Backend.Cli.Commands.Map;

using global::System.ComponentModel;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class MapOpenCommand(IMapControlService mapControlService) : AsyncCommand<MapOpenCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--room <NAME>")]
        [Description("Room to open.")]
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
        await mapControlService.OpenRoomAsync(settings.RoomName.Trim(), cancellationToken).ConfigureAwait(false);
        AnsiConsole.MarkupLine($"[green]Room {settings.RoomName} opened.[/]");
        return 0;
    }
}
