namespace ScreepsDotNet.Backend.Cli.Commands.Invader;

using global::System.ComponentModel;
using ScreepsDotNet.Backend.Cli.Formatting;
using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Parsing;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class InvaderCreateCommand(IInvaderService invaderService, IUserRepository userRepository, ILogger<InvaderCreateCommand>? logger = null, IHostApplicationLifetime? lifetime = null, ICommandOutputFormatter? outputFormatter = null) : CommandHandler<InvaderCreateCommand.Settings>(logger, lifetime, outputFormatter)
{
    public sealed class Settings : FormattableCommandSettings
    {
        [CommandOption("--user-id <ID>")]
        [Description("Caller user ID (for summoner tracking).")]
        public string? UserId { get; init; }

        [CommandOption("--username <NAME>")]
        [Description("Caller username.")]
        public string? Username { get; init; }

        [CommandOption("--room <NAME>")]
        [Description("Room name where to create invader.")]
        public string RoomName { get; init; } = string.Empty;

        [CommandOption("--shard <NAME>")]
        [Description("Optional shard name (legacy shard/RoomName notation is also accepted).")]
        public string? Shard { get; init; }

        [CommandOption("-x|--pos-x <COORD>")]
        [Description("X coordinate (0-49).")]
        public int X { get; init; }

        [CommandOption("-y|--pos-y <COORD>")]
        [Description("Y coordinate (0-49).")]
        public int Y { get; init; }

        [CommandOption("--type <TYPE>")]
        [Description("Invader type (Healer, Ranged, Melee).")]
        [DefaultValue(InvaderType.Melee)]
        public InvaderType Type { get; init; }

        [CommandOption("--size <SIZE>")]
        [Description("Invader size (Small, Big).")]
        [DefaultValue(InvaderSize.Small)]
        public InvaderSize Size { get; init; }

        [CommandOption("--boosted")]
        [Description("Whether the invader should be boosted.")]
        public bool Boosted { get; init; }

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

            if (!RoomReferenceParser.TryParse(RoomName, Shard, out _))
                return ValidationResult.Error("Invalid room. Use W##N## or shard/W##N##.");

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
            var publicProfile = await userRepository.FindPublicProfileAsync(settings.Username, null, cancellationToken).ConfigureAwait(false);
            if (publicProfile is null) {
                if (settings.OutputJson)
                    OutputFormatter.WriteJson(new { success = false, error = $"User '{settings.Username}' not found." });
                else
                    OutputFormatter.WriteMarkupLine($"[red]Error:[/] User '{settings.Username}' not found.");
                return 1;
            }
            userId = publicProfile.Id;
        }

        if (!RoomReferenceParser.TryParse(settings.RoomName, settings.Shard, out var reference) || reference is null) {
            if (settings.OutputJson)
                OutputFormatter.WriteJson(new { success = false, error = "Invalid room. Use W##N## or shard/W##N##." });
            else
                OutputFormatter.WriteMarkupLine("[red]Error:[/] Invalid room. Use W##N## or shard/W##N##.");
            return 1;
        }

        var request = new CreateInvaderRequest(reference.RoomName, settings.X, settings.Y, settings.Type, settings.Size, settings.Boosted, reference.ShardName);
        var result = await invaderService.CreateInvaderAsync(userId, request, cancellationToken).ConfigureAwait(false);

        if (result.Status != CreateInvaderResultStatus.Success) {
            if (settings.OutputJson)
                OutputFormatter.WriteJson(new { success = false, error = result.ErrorMessage ?? result.Status.ToString() });
            else
                OutputFormatter.WriteMarkupLine($"[red]Error:[/] {result.Status} {result.ErrorMessage}");
            return 1;
        }

        if (settings.OutputJson) {
            OutputFormatter.WriteJson(new
            {
                success = true,
                result.Id,
                Room = reference.RoomName,
                reference.ShardName,
                settings.X,
                settings.Y,
                settings.Type,
                settings.Size,
                settings.Boosted
            });
            return 0;
        }

        OutputFormatter.WriteKeyValueTable([
                                               ("Invader ID", result.Id ?? "(unknown)"),
                                               ("Room", reference.ShardName is null ? reference.RoomName : $"{reference.ShardName}/{reference.RoomName}"),
                                               ("Position", $"({settings.X}, {settings.Y})"),
                                               ("Type", settings.Type.ToString()),
                                               ("Size", settings.Size.ToString()),
                                               ("Boosted", settings.Boosted ? "yes" : "no")
                                           ],
                                           "Invader created");
        return 0;
    }
}
