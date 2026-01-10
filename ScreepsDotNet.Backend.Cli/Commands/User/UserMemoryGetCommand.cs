namespace ScreepsDotNet.Backend.Cli.Commands.User;

using global::System.Text.Json;
using ScreepsDotNet.Backend.Cli.Formatting;
using ScreepsDotNet.Backend.Core.Repositories;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class UserMemoryGetCommand(IUserMemoryRepository memoryRepository, ILogger<UserMemoryGetCommand>? logger = null, IHostApplicationLifetime? lifetime = null, ICommandOutputFormatter? outputFormatter = null) : CommandHandler<UserMemoryGetCommand.Settings>(logger, lifetime, outputFormatter)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public sealed class Settings : FormattableCommandSettings
    {
        [CommandOption("--user-id <ID>")]
        public string? UserId { get; init; }

        [CommandOption("--segment <ID>")]
        public int? Segment { get; init; }

        [CommandOption("--json")]
        public bool OutputJson { get; init; }

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(UserId))
                return ValidationResult.Error("Specify --user-id.");

            if (Segment is { } segment && (segment < 0 || segment > 99))
                return ValidationResult.Error("Segment must be between 0 and 99.");

            return ValidationResult.Success();
        }
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (settings.Segment is { } segment) {
            var data = await memoryRepository.GetMemorySegmentAsync(settings.UserId!, segment, cancellationToken).ConfigureAwait(false);
            if (settings.OutputJson) {
                var payload = new { segment, data };
                OutputFormatter.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
            }
            else {
                OutputFormatter.WriteKeyValueTable([($"Segment {segment}", data ?? "(null)")],
                                                   "Memory segment");
            }

            return 0;
        }

        var memory = await memoryRepository.GetMemoryAsync(settings.UserId!, cancellationToken).ConfigureAwait(false);

        if (settings.OutputJson) {
            OutputFormatter.WriteLine(JsonSerializer.Serialize(memory, JsonOptions));
            return 0;
        }

        var rows = memory.Select(entry => (IReadOnlyList<string>)[entry.Key, JsonSerializer.Serialize(entry.Value, JsonOptions)]);
        OutputFormatter.WriteTabularData("User memory", ["Key", "Value"], rows);
        return 0;
    }
}
