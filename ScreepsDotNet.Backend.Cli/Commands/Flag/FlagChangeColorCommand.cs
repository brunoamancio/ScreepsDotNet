namespace ScreepsDotNet.Backend.Cli.Commands.Flag;

using global::System.ComponentModel;
using ScreepsDotNet.Backend.Cli.Formatting;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class FlagChangeColorCommand(IFlagService flagService, IUserRepository userRepository, ILogger<FlagChangeColorCommand>? logger = null, IHostApplicationLifetime? lifetime = null, ICommandOutputFormatter? outputFormatter = null) : CommandHandler<FlagChangeColorCommand.Settings>(logger, lifetime, outputFormatter)
{
    public sealed class Settings : FormattableCommandSettings
    {
        [CommandOption("--user-id <ID>")]
        [Description("The ID of the user.")]
        public string? UserId { get; init; }

        [CommandOption("--username <NAME>")]
        [Description("The username of the user.")]
        public string? Username { get; init; }

        [CommandOption("--room <NAME>")]
        [Description("The room name (e.g., W1N1).")]
        public string RoomName { get; init; } = string.Empty;

        [CommandOption("--shard <NAME>")]
        [Description("Optional shard name (e.g., shard1).")]
        public string? Shard { get; init; }

        [CommandOption("--name <NAME>")]
        [Description("Unique name for the flag.")]
        public string Name { get; init; } = string.Empty;

        [CommandOption("--color <COLOR>")]
        [Description("Primary color (Red, Purple, Blue, Cyan, Green, Yellow, Orange, Brown, Grey, White).")]
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

            return ValidationResult.Success();
        }
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var userId = settings.UserId;
        if (string.IsNullOrWhiteSpace(userId)) {
            var profile = await userRepository.FindPublicProfileAsync(settings.Username, null, cancellationToken).ConfigureAwait(false);
            if (profile is null) {
                if (settings.OutputJson)
                    OutputFormatter.WriteJson(new { success = false, error = "User not found." });
                else
                    OutputFormatter.WriteMarkupLine("[red]Error:[/] User not found.");
                return 1;
            }
            userId = profile.Id;
        }

        var result = await flagService.ChangeFlagColorAsync(
            userId,
            settings.RoomName,
            settings.Shard,
            settings.Name,
            settings.Color,
            settings.SecondaryColor ?? settings.Color,
            cancellationToken
        );

        if (result.Status != FlagResultStatus.Success) {
            if (settings.OutputJson)
                OutputFormatter.WriteJson(new { success = false, error = result.ErrorMessage ?? result.Status.ToString() });
            else
                OutputFormatter.WriteMarkupLine($"[red]Error:[/] {result.ErrorMessage ?? result.Status.ToString()}");
            return 1;
        }

        if (settings.OutputJson) {
            OutputFormatter.WriteJson(new
            {
                success = true,
                settings.Name,
                settings.RoomName,
                settings.Shard,
                Primary = settings.Color,
                Secondary = settings.SecondaryColor ?? settings.Color
            });
            return 0;
        }

        OutputFormatter.WriteKeyValueTable([
                                               ("Flag", settings.Name),
                                               ("Primary Color", settings.Color.ToString()),
                                               ("Secondary Color", (settings.SecondaryColor ?? settings.Color).ToString())
                                           ],
                                           "Flag color change");
        return 0;
    }
}
