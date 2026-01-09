namespace ScreepsDotNet.Backend.Cli.Commands.Map;

using global::System;
using global::System.ComponentModel;
using ScreepsDotNet.Backend.Core.Parsing;
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

        [CommandOption("--shard <NAME>")]
        [Description("Optional shard override (e.g., shard3).")]
        public string? Shard { get; init; }

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(RoomName))
                return ValidationResult.Error("Room name is required.");

            if (!RoomReferenceParser.TryParse(RoomName, Shard, out _))
                return ValidationResult.Error("Room name must match W##N## (optionally shard/W##N##).");

            return ValidationResult.Success();
        }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (!RoomReferenceParser.TryParse(settings.RoomName, settings.Shard, out var reference) || reference is null)
            throw new InvalidOperationException("Room name validation failed.");

        await mapControlService.CloseRoomAsync(reference.RoomName, cancellationToken).ConfigureAwait(false);
        AnsiConsole.MarkupLine($"[yellow]Room {reference.RoomName} closed.[/]");
        return 0;
    }
}
