namespace ScreepsDotNet.Backend.Cli.Commands.Flag;

using global::System.ComponentModel;
using ScreepsDotNet.Backend.Cli.Formatting;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class FlagCreateCommand(IFlagService flagService, IUserRepository userRepository, ILogger<FlagCreateCommand>? logger = null, IHostApplicationLifetime? lifetime = null, ICommandOutputFormatter? outputFormatter = null) : CommandHandler<FlagCreateCommand.Settings>(logger, lifetime, outputFormatter)
{
    public sealed class Settings : FormattableCommandSettings
    {
        [CommandOption("--user-id <ID>")]
        [Description("The ID of the user creating the flag.")]
        public string? UserId { get; init; }

        [CommandOption("--username <NAME>")]
        [Description("The username of the user creating the flag.")]
        public string? Username { get; init; }

        [CommandOption("--room <NAME>")]
        [Description("The room name (e.g., W1N1).")]
        public string RoomName { get; init; } = string.Empty;

        [CommandOption("--shard <NAME>")]
        [Description("Optional shard name (e.g., shard1).")]
        public string? Shard { get; init; }

        [CommandOption("-x|--pos-x <COORD>")]
        [Description("X coordinate (0-49).")]
        public int X { get; init; }

        [CommandOption("-y|--pos-y <COORD>")]
        [Description("Y coordinate (0-49).")]
        public int Y { get; init; }

        [CommandOption("--name <NAME>")]
        [Description("Unique name for the flag.")]
        public string Name { get; init; } = string.Empty;

        [CommandOption("--color <COLOR>")]
        [Description("Primary color (Red, Purple, Blue, Cyan, Green, Yellow, Orange, Brown, Grey, White).")]
        [DefaultValue(Core.Constants.Color.White)]
        public Core.Constants.Color Color { get; init; }

        [CommandOption("--secondary-color <COLOR>")]
        [Description("Secondary color (defaults to primary color).")]
        public Core.Constants.Color? SecondaryColor { get; init; }

        [CommandOption("--json")]
        public bool OutputJson { get; init; }

        public override ValidationResult Validate()
        {
            var formatResult = base.Validate();
            if (!formatResult.Successful)
                return formatResult;

            if (string.IsNullOrWhiteSpace(UserId) && string.IsNullOrWhiteSpace(Username))
                return ValidationResult.Error("Either --user-id or --username must be provided.");

            if (string.IsNullOrWhiteSpace(RoomName))
                return ValidationResult.Error("Room name is required.");

            if (string.IsNullOrWhiteSpace(Name))
                return ValidationResult.Error("Flag name is required.");

            if (X is < 0 or > 49)
                return ValidationResult.Error("X must be between 0 and 49.");

            if (Y is < 0 or > 49)
                return ValidationResult.Error("Y must be between 0 and 49.");

            return ValidationResult.Success();
        }
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var userId = settings.UserId;
        if (string.IsNullOrWhiteSpace(userId)) {
            var profile = await userRepository.FindPublicProfileAsync(settings.Username, null, cancellationToken).ConfigureAwait(false);
            if (profile is null) {
                if (settings.OutputJson) {
                    OutputFormatter.WriteJson(new { success = false, error = "User not found." });
                }
                else
                    OutputFormatter.WriteMarkupLine("[red]Error:[/] User not found.");
                return 1;
            }
            userId = profile.Id;
        }

        var request = new CreateFlagRequest(
            settings.RoomName,
            settings.X,
            settings.Y,
            settings.Name,
            settings.Color,
            settings.SecondaryColor ?? settings.Color,
            settings.Shard
        );

        var result = await flagService.CreateFlagAsync(userId, request, cancellationToken).ConfigureAwait(false);

        if (result.Status != FlagResultStatus.Success) {
            if (settings.OutputJson)
                OutputFormatter.WriteJson(new { success = false, error = result.ErrorMessage ?? result.Status.ToString() });
            else
                OutputFormatter.WriteMarkupLine($"[red]Error:[/] {result.ErrorMessage ?? result.Status.ToString()}");
            return 1;
        }

        var shardLabel = string.IsNullOrWhiteSpace(settings.Shard) ? "default shard" : settings.Shard;
        if (settings.OutputJson) {
            OutputFormatter.WriteJson(new
            {
                success = true,
                settings.Name,
                settings.RoomName,
                Shard = settings.Shard,
                settings.X,
                settings.Y
            });
            return 0;
        }

        OutputFormatter.WriteKeyValueTable([
                                               ("Flag", settings.Name),
                                               ("Room", $"{settings.RoomName} ({shardLabel})"),
                                               ("Position", $"({settings.X}, {settings.Y})")
                                           ],
                                           "Flag created");
        return 0;
    }
}
