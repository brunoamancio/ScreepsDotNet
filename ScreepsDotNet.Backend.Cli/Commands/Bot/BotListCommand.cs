namespace ScreepsDotNet.Backend.Cli.Commands.Bot;

using global::System;
using global::System.Globalization;
using global::System.Linq;
using global::System.Text.Json;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class BotListCommand(IBotDefinitionProvider definitionProvider, ILogger<BotListCommand>? logger = null, IHostApplicationLifetime? lifetime = null) : CommandHandler<BotListCommand.Settings>(logger, lifetime)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--json")]
        public bool OutputJson { get; init; }
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var definitions = await definitionProvider.GetDefinitionsAsync(cancellationToken).ConfigureAwait(false);

        if (settings.OutputJson) {
            var payload = definitions
                          .OrderBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
                          .Select(definition => new
                          {
                              definition.Name,
                              definition.Description,
                              ModuleCount = definition.Modules.Count
                          });

            AnsiConsole.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
            return 0;
        }

        if (definitions.Count == 0) {
            AnsiConsole.MarkupLine("[yellow]No bot AI definitions were found. Use --modfile to point at mods.json.[/]");
            return 0;
        }

        var table = new Table().AddColumn("Bot").AddColumn("Description").AddColumn("Modules");
        foreach (var definition in definitions.OrderBy(def => def.Name, StringComparer.OrdinalIgnoreCase))
            table.AddRow(definition.Name, definition.Description, definition.Modules.Count.ToString(CultureInfo.InvariantCulture));

        AnsiConsole.Write(table);
        return 0;
    }
}
