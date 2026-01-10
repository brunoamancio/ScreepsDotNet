namespace ScreepsDotNet.Backend.Cli.Commands.World;

using global::System.Collections.Generic;
using global::System.ComponentModel;
using ScreepsDotNet.Backend.Cli.Formatting;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Parsing;
using ScreepsDotNet.Backend.Core.Repositories;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class WorldOverviewCommand(
    IRoomOverviewRepository overviewRepository,
    ILogger<WorldOverviewCommand>? logger = null,
    IHostApplicationLifetime? lifetime = null,
    ICommandOutputFormatter? outputFormatter = null)
    : CommandHandler<WorldOverviewCommand.Settings>(logger, lifetime, outputFormatter)
{
    public sealed class Settings : FormattableCommandSettings
    {
        [CommandOption("--room <NAME>")]
        [Description("Room identifier (e.g., W1N1).")]
        public string RoomName { get; init; } = string.Empty;

        [CommandOption("--shard <NAME>")]
        [Description("Optional shard override (e.g., shard2).")]
        public string? Shard { get; init; }

        [CommandOption("--json")]
        [Description("Emit JSON payload.")]
        public bool OutputJson { get; init; }

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(RoomName))
                return ValidationResult.Error("Specify --room.");

            if (!RoomReferenceParser.TryParse(RoomName, Shard, out _))
                return ValidationResult.Error("Room must match W##N## (optionally shard/W##N##).");

            return ValidationResult.Success();
        }
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (!RoomReferenceParser.TryParse(settings.RoomName, settings.Shard, out var reference) || reference is null)
            throw new InvalidOperationException("Room validation failed.");

        var overview = await overviewRepository.GetRoomOverviewAsync(reference, cancellationToken).ConfigureAwait(false);

        if (settings.OutputJson) {
            OutputFormatter.WriteJson(new
            {
                room = new { reference.RoomName, reference.ShardName },
                overview?.Owner
            });
            return 0;
        }

        if (overview?.Owner is null) {
            OutputFormatter.WriteKeyValueTable([("Room", FormatRoom(reference)), ("Owner", "(unowned)")]);
            return 0;
        }

        OutputFormatter.WriteTabularData("Room overview",
                                         ["Room", "Owner", "User Id"],
                                         [[FormatRoom(reference), overview.Owner.Username, overview.Owner.Id]]);
        return 0;
    }

    private static string FormatRoom(RoomReference reference)
        => string.IsNullOrWhiteSpace(reference.ShardName) ? reference.RoomName : $"{reference.ShardName}/{reference.RoomName}";
}
