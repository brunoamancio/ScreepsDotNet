namespace ScreepsDotNet.Backend.Cli.Commands.Stronghold;

using global::System;
using global::System.ComponentModel;
using global::System.Text.Json;
using ScreepsDotNet.Backend.Cli.Formatting;
using ScreepsDotNet.Backend.Core.Models.Strongholds;
using ScreepsDotNet.Backend.Core.Parsing;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class StrongholdSpawnCommand(IStrongholdControlService strongholdControlService, ILogger<StrongholdSpawnCommand>? logger = null, IHostApplicationLifetime? lifetime = null, ICommandOutputFormatter? outputFormatter = null) : CommandHandler<StrongholdSpawnCommand.Settings>(logger, lifetime, outputFormatter)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public sealed class Settings : FormattableCommandSettings
    {
        [CommandOption("--room <NAME>")]
        [Description("Room to deploy the stronghold (e.g., W5N3).")]
        public string RoomName { get; init; } = string.Empty;

        [CommandOption("--template <NAME>")]
        [Description("Specific template name (default random).")]
        public string? TemplateName { get; init; }

        [CommandOption("--shard <NAME>")]
        [Description("Optional shard (e.g., shard2).")]
        public string? Shard { get; init; }

        [CommandOption("-x|--pos-x <COORD>")]
        [Description("Origin X coordinate (0-49). Requires -y/--pos-y.")]
        public int? X { get; init; }

        [CommandOption("-y|--pos-y <COORD>")]
        [Description("Origin Y coordinate (0-49). Requires -x/--pos-x.")]
        public int? Y { get; init; }

        [CommandOption("--owner <USERID>")]
        [Description("Override owner user id (default invader user).")]
        public string? OwnerUserId { get; init; }

        [CommandOption("--deploy-delay <TICKS>")]
        [Description("Delay before deployment (ticks, default 1).")]
        public int? DeployDelayTicks { get; init; }

        [CommandOption("--json")]
        public bool OutputJson { get; init; }

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(RoomName))
                return ValidationResult.Error("Room name is required.");

            if (X.HasValue ^ Y.HasValue)
                return ValidationResult.Error("Both -x/--pos-x and -y/--pos-y must be specified together.");

            if (X is < 0 or > 49)
                return ValidationResult.Error("X must be within 0-49.");

            if (Y is < 0 or > 49)
                return ValidationResult.Error("Y must be within 0-49.");

            if (DeployDelayTicks is < 0)
                return ValidationResult.Error("Deploy delay must be zero or positive.");

            if (!RoomReferenceParser.TryParse(RoomName, Shard, out _))
                return ValidationResult.Error("Room must match W##N## (optionally shard/W##N##).");

            return ValidationResult.Success();
        }
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (!RoomReferenceParser.TryParse(settings.RoomName, settings.Shard, out var reference) || reference is null)
            throw new InvalidOperationException("Room validation failed.");

        var options = new StrongholdSpawnOptions(settings.TemplateName,
                                                 settings.X,
                                                 settings.Y,
                                                 settings.OwnerUserId,
                                                 settings.DeployDelayTicks);

        var result = await strongholdControlService.SpawnAsync(reference.RoomName, reference.ShardName, options, cancellationToken).ConfigureAwait(false);

        if (settings.OutputJson) {
            OutputFormatter.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            return 0;
        }

        OutputFormatter.WriteKeyValueTable([
                                               ("Room", string.IsNullOrWhiteSpace(result.ShardName) ? result.RoomName : $"{result.ShardName}/{result.RoomName}"),
                                               ("Template", result.TemplateName),
                                               ("Stronghold ID", result.InvaderCoreId)
                                           ],
                                           "Stronghold spawn");

        return 0;
    }
}
