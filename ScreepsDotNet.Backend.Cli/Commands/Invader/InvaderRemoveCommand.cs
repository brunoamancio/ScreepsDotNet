namespace ScreepsDotNet.Backend.Cli.Commands.Invader;

using global::System.ComponentModel;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class InvaderRemoveCommand(IInvaderService invaderService, IUserRepository userRepository, ILogger<InvaderRemoveCommand>? logger = null, IHostApplicationLifetime? lifetime = null, ICommandOutputFormatter? outputFormatter = null) : CommandHandler<InvaderRemoveCommand.Settings>(logger, lifetime, outputFormatter)
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--user-id <ID>")]
        [Description("Caller user ID (for summoner tracking).")]
        public string? UserId { get; init; }

        [CommandOption("--username <NAME>")]
        [Description("Caller username.")]
        public string? Username { get; init; }

        [CommandOption("--id <INVADER_ID>")]
        [Description("The ID of the invader to remove.")]
        public string InvaderId { get; init; } = string.Empty;

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(UserId) && string.IsNullOrWhiteSpace(Username))
                return ValidationResult.Error("Either --user-id or --username must be provided.");

            if (string.IsNullOrWhiteSpace(InvaderId))
                return ValidationResult.Error("Invader ID is required.");

            return ValidationResult.Success();
        }
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var userId = settings.UserId;
        if (string.IsNullOrWhiteSpace(userId)) {
            var publicProfile = await userRepository.FindPublicProfileAsync(settings.Username, null, cancellationToken).ConfigureAwait(false);
            if (publicProfile is null) {
                OutputFormatter.WriteMarkupLine($"[red]Error:[/] User '{settings.Username}' not found.");
                return 1;
            }
            userId = publicProfile.Id;
        }

        var request = new RemoveInvaderRequest(settings.InvaderId);
        var result = await invaderService.RemoveInvaderAsync(userId, request, cancellationToken).ConfigureAwait(false);

        if (result.Status != RemoveInvaderResultStatus.Success) {
            OutputFormatter.WriteMarkupLine($"[red]Error:[/] {result.Status}");
            return 1;
        }

        OutputFormatter.WriteMarkupLine($"[green]Success:[/] Invader removed.");
        return 0;
    }
}
