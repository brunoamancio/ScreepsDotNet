namespace ScreepsDotNet.Backend.Cli.Commands.Map;

using global::System;
using global::System.ComponentModel;
using ScreepsDotNet.Backend.Cli.Formatting;
using ScreepsDotNet.Backend.Core.Parsing;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class MapAssetsUpdateCommand(IMapControlService mapControlService, ILogger<MapAssetsUpdateCommand>? logger = null, IHostApplicationLifetime? lifetime = null, ICommandOutputFormatter? outputFormatter = null) : CommandHandler<MapAssetsUpdateCommand.Settings>(logger, lifetime, outputFormatter)
{
    public sealed class Settings : FormattableCommandSettings
    {
        [CommandOption("--room <NAME>")]
        [Description("Room whose assets should be regenerated.")]
        public string RoomName { get; init; } = string.Empty;

        [CommandOption("--full")]
        [Description("Force full regeneration instead of incremental update.")]
        public bool Full { get; init; }

        [CommandOption("--shard <NAME>")]
        [Description("Optional shard override (e.g., shard3).")]
        public string? Shard { get; init; }

        [CommandOption("--json")]
        public bool OutputJson { get; init; }

        public override ValidationResult Validate()
        {
            var formatResult = base.Validate();
            if (!formatResult.Successful)
                return formatResult;

            if (string.IsNullOrWhiteSpace(RoomName))
                return ValidationResult.Error("Room name is required.");

            if (!RoomReferenceParser.TryParse(RoomName, Shard, out _))
                return ValidationResult.Error("Room name must match W##N## (optionally shard/W##N##).");

            return ValidationResult.Success();
        }
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (!RoomReferenceParser.TryParse(settings.RoomName, settings.Shard, out var reference) || reference is null)
            throw new InvalidOperationException("Room name validation failed.");

        await mapControlService.UpdateRoomAssetsAsync(reference.RoomName, reference.ShardName, settings.Full, cancellationToken).ConfigureAwait(false);
        var displayName = string.IsNullOrWhiteSpace(reference.ShardName) ? reference.RoomName : $"{reference.ShardName}/{reference.RoomName}";

        if (settings.OutputJson) {
            OutputFormatter.WriteJson(new { Room = reference.RoomName, reference.ShardName, settings.Full, queued = true });
            return 0;
        }

        OutputFormatter.WriteKeyValueTable([
                                               ("Room", displayName),
                                               ("Full Refresh", settings.Full ? "yes" : "no"),
                                               ("Queued", "yes")
                                           ],
                                           "Map assets");
        return 0;
    }
}
