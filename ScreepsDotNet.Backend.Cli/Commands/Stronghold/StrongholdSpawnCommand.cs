namespace ScreepsDotNet.Backend.Cli.Commands.Stronghold;

using global::System.ComponentModel;
using global::System.Text.Json;
using ScreepsDotNet.Backend.Core.Models.Strongholds;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class StrongholdSpawnCommand(IStrongholdControlService strongholdControlService) : AsyncCommand<StrongholdSpawnCommand.Settings>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--room <NAME>")]
        [Description("Room to deploy the stronghold (e.g., W5N3).")]
        public string RoomName { get; init; } = string.Empty;

        [CommandOption("--template <NAME>")]
        [Description("Specific template name (default random).")]
        public string? TemplateName { get; init; }

        [CommandOption("--x <COORD>")]
        [Description("Origin X coordinate (0-49). Requires --y.")]
        public int? X { get; init; }

        [CommandOption("--y <COORD>")]
        [Description("Origin Y coordinate (0-49). Requires --x.")]
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
                return ValidationResult.Error("Both --x and --y must be specified together.");

            if (X is < 0 or > 49)
                return ValidationResult.Error("X must be within 0-49.");

            if (Y is < 0 or > 49)
                return ValidationResult.Error("Y must be within 0-49.");

            if (DeployDelayTicks is < 0)
                return ValidationResult.Error("Deploy delay must be zero or positive.");

            return ValidationResult.Success();
        }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var options = new StrongholdSpawnOptions(settings.TemplateName,
                                                 settings.X,
                                                 settings.Y,
                                                 settings.OwnerUserId,
                                                 settings.DeployDelayTicks);

        var result = await strongholdControlService.SpawnAsync(settings.RoomName, options, cancellationToken).ConfigureAwait(false);

        if (settings.OutputJson) {
            AnsiConsole.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            return 0;
        }

        var table = new Table().AddColumn("Property").AddColumn("Value");
        table.AddRow("Room", result.RoomName);
        table.AddRow("Template", result.TemplateName);
        table.AddRow("Stronghold ID", result.InvaderCoreId);
        AnsiConsole.Write(table);

        return 0;
    }
}
