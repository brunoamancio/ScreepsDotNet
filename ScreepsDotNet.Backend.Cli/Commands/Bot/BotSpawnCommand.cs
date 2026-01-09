namespace ScreepsDotNet.Backend.Cli.Commands.Bot;

using global::System.ComponentModel;
using global::System.Globalization;
using global::System.Text.Json;
using ScreepsDotNet.Backend.Core.Models.Bots;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class BotSpawnCommand(IBotControlService botControlService, ILogger<BotSpawnCommand>? logger = null, IHostApplicationLifetime? lifetime = null) : CommandHandler<BotSpawnCommand.Settings>(logger, lifetime)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--bot <NAME>")]
        [Description("Bot AI definition name (from mods.json).")]
        public string BotName { get; init; } = string.Empty;

        [CommandOption("--room <NAME>")]
        [Description("Room name where the bot should spawn (e.g., W1N1).")]
        public string RoomName { get; init; } = string.Empty;

        [CommandOption("--shard <NAME>")]
        [Description("Optional shard name (e.g., shard1).")]
        public string? Shard { get; init; }

        [CommandOption("--username <NAME>")]
        [Description("Custom username for the bot (default is random).")]
        public string? Username { get; init; }

        [CommandOption("--cpu <LIMIT>")]
        [Description("CPU limit for the bot (defaults to 100).")]
        public int? Cpu { get; init; }

        [CommandOption("--gcl <LEVEL>")]
        [Description("Initial Global Control Level (default: 1).")]
        public int? GlobalControlLevel { get; init; }

        [CommandOption("--x <COORD>")]
        [Description("Spawn X coordinate (0-49). Requires --y.")]
        public int? SpawnX { get; init; }

        [CommandOption("--y <COORD>")]
        [Description("Spawn Y coordinate (0-49). Requires --x.")]
        public int? SpawnY { get; init; }

        [CommandOption("--json")]
        public bool OutputJson { get; init; }

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(BotName))
                return ValidationResult.Error("Bot AI name is required.");

            if (string.IsNullOrWhiteSpace(RoomName))
                return ValidationResult.Error("Room name is required.");

            if (SpawnX.HasValue ^ SpawnY.HasValue)
                return ValidationResult.Error("Both --x and --y must be set together.");

            if (SpawnX is < 0 or > 49)
                return ValidationResult.Error("Spawn X must be between 0 and 49.");

            if (SpawnY is < 0 or > 49)
                return ValidationResult.Error("Spawn Y must be between 0 and 49.");

            if (Cpu is <= 0)
                return ValidationResult.Error("CPU limit must be a positive integer.");

            if (GlobalControlLevel is <= 0)
                return ValidationResult.Error("GCL must be a positive integer.");

            return ValidationResult.Success();
        }
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var options = new BotSpawnOptions(settings.Username,
                                          settings.Cpu,
                                          settings.GlobalControlLevel,
                                          settings.SpawnX,
                                          settings.SpawnY);

        var result = await botControlService.SpawnAsync(settings.BotName, settings.RoomName, settings.Shard, options, cancellationToken).ConfigureAwait(false);

        if (settings.OutputJson) {
            var payload = new
            {
                result.UserId,
                result.Username,
                result.RoomName,
                result.ShardName,
                Spawn = new { result.SpawnX, result.SpawnY }
            };
            AnsiConsole.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
            return 0;
        }

        var table = new Table().AddColumn("Property").AddColumn("Value");
        table.AddRow("User ID", result.UserId);
        table.AddRow("Username", result.Username);
        table.AddRow("Room", result.RoomName);
        table.AddRow("Shard", result.ShardName ?? "default");
        table.AddRow("Spawn", $"({result.SpawnX.ToString(CultureInfo.InvariantCulture)},{result.SpawnY.ToString(CultureInfo.InvariantCulture)})");
        AnsiConsole.Write(table);

        return 0;
    }
}
