namespace ScreepsDotNet.Backend.Cli.Commands.Flag;

using global::System.ComponentModel;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Backend.Core.Repositories;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class FlagRemoveCommand(IFlagService flagService, IUserRepository userRepository) : AsyncCommand<FlagRemoveCommand.Settings>
{
    public sealed class Settings : CommandSettings
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

        [CommandOption("--name <NAME>")]
        [Description("Unique name for the flag.")]
        public string Name { get; init; } = string.Empty;

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(UserId) && string.IsNullOrWhiteSpace(Username))
                return ValidationResult.Error("Either --user-id or --username must be provided.");

            if (string.IsNullOrWhiteSpace(RoomName))
                return ValidationResult.Error("Room name is required.");

            if (string.IsNullOrWhiteSpace(Name))
                return ValidationResult.Error("Flag name is required.");

            return ValidationResult.Success();
        }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var userId = settings.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            var profile = await userRepository.FindPublicProfileAsync(settings.Username, null, cancellationToken).ConfigureAwait(false);
            if (profile is null)
            {
                AnsiConsole.MarkupLine("[red]Error:[/] User not found.");
                return 1;
            }
            userId = profile.Id;
        }

        var result = await flagService.RemoveFlagAsync(
            userId,
            settings.RoomName,
            settings.Name,
            cancellationToken
        );

        if (result.Status != FlagResultStatus.Success)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {result.ErrorMessage ?? result.Status.ToString()}");
            return 1;
        }

        AnsiConsole.MarkupLine($"[green]Success:[/] Flag [yellow]{settings.Name}[/] removed from [blue]{settings.RoomName}[/].");
        return 0;
    }
}
