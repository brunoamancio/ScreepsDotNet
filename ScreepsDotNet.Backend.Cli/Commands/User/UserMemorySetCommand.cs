namespace ScreepsDotNet.Backend.Cli.Commands.User;

using global::System.Text.Json;
using Microsoft.Extensions.Logging;
using ScreepsDotNet.Backend.Core.Repositories;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class UserMemorySetCommand(IUserMemoryRepository memoryRepository, ILogger<UserMemorySetCommand>? logger = null, IHostApplicationLifetime? lifetime = null)
    : CommandHandler<UserMemorySetCommand.Settings>(logger, lifetime)
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--user-id <ID>")]
        public string? UserId { get; init; }

        [CommandOption("--path <PATH>")]
        public string? Path { get; init; }

        [CommandOption("--value <JSON>")]
        public string? JsonValue { get; init; }

        [CommandOption("--segment <ID>")]
        public int? Segment { get; init; }

        [CommandOption("--data <STRING>")]
        public string? SegmentData { get; init; }

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(UserId))
                return ValidationResult.Error("Specify --user-id.");

            if (Segment is int segment) {
                if (segment is < 0 or > 99)
                    return ValidationResult.Error("Segment must be between 0 and 99.");
                if (SegmentData is null)
                    return ValidationResult.Error("Specify --data when writing a segment.");
                return ValidationResult.Success();
            }

            if (JsonValue is null)
                return ValidationResult.Error("Specify --value containing JSON content.");

            return ValidationResult.Success();
        }
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (settings.Segment is { } segment) {
            await memoryRepository.SetMemorySegmentAsync(settings.UserId!, segment, settings.SegmentData, cancellationToken).ConfigureAwait(false);
            if (settings.UserId == null) return 0;

            AnsiConsole.MarkupLine("[green]Updated memory segment {0} for {1}.[/]", segment, settings.UserId);
            Logger.LogInformation("Updated segment {Segment} for {UserId}.", segment, settings.UserId);
            return 0;
        }

        var json = ParseJsonElement(settings.JsonValue!);
        await memoryRepository.UpdateMemoryAsync(settings.UserId!, settings.Path, json, cancellationToken).ConfigureAwait(false);
        if (settings.UserId == null) return 0;

        AnsiConsole.MarkupLine("[green]Updated memory for {0} at path '{1}'.[/]", settings.UserId, settings.Path ?? "(root)");
        Logger.LogInformation("Updated memory for {UserId} at path {Path}.", settings.UserId, settings.Path ?? "(root)");
        return 0;
    }

    private static JsonElement ParseJsonElement(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }
}
