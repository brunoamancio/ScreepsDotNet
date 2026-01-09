namespace ScreepsDotNet.Backend.Cli.Commands.Map;

using global::System;
using global::System.ComponentModel;
using ScreepsDotNet.Backend.Core.Parsing;
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

        await mapControlService.RemoveRoomAsync(reference.RoomName, reference.ShardName, settings.PurgeObjects, cancellationToken).ConfigureAwait(false);
        var displayName = string.IsNullOrWhiteSpace(reference.ShardName) ? reference.RoomName : $"{reference.ShardName}/{reference.RoomName}";
        AnsiConsole.MarkupLine($"[red]Room {displayName} removed (purge={settings.PurgeObjects}).[/]");
        return 0;
    }
}
