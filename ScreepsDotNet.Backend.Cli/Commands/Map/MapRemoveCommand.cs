namespace ScreepsDotNet.Backend.Cli.Commands.Map;

using global::System.ComponentModel;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class MapRemoveCommand(IMapControlService mapControlService) : AsyncCommand<MapRemoveCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--room <NAME>")]
        [Description("Room to remove.")]
        public string RoomName { get; init; } = string.Empty;

        [CommandOption("--purge-objects")]
        [Description("Also remove room objects.")]
        public bool PurgeObjects { get; init; }

        public override ValidationResult Validate()
        {
            return string.IsNullOrWhiteSpace(RoomName)
                ? ValidationResult.Error("Room name is required.")
                : ValidationResult.Success();
        }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        await mapControlService.RemoveRoomAsync(settings.RoomName.Trim(), settings.PurgeObjects, cancellationToken).ConfigureAwait(false);
        AnsiConsole.MarkupLine($"[red]Room {settings.RoomName} removed (purge={settings.PurgeObjects}).[/]");
        return 0;
    }
}
