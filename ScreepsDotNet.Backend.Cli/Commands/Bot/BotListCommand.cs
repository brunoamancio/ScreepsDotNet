namespace ScreepsDotNet.Backend.Cli.Commands.Bot;

using global::System;
using global::System.Globalization;
using global::System.Linq;
using global::System.Text.Json;
using ScreepsDotNet.Backend.Cli.Formatting;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console.Cli;

internal sealed class BotListCommand(IBotDefinitionProvider definitionProvider, ILogger<BotListCommand>? logger = null, IHostApplicationLifetime? lifetime = null, ICommandOutputFormatter? outputFormatter = null) : CommandHandler<BotListCommand.Settings>(logger, lifetime, outputFormatter)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public sealed class Settings : FormattableCommandSettings
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

            OutputFormatter.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
            return 0;
        }

        if (definitions.Count == 0) {
            OutputFormatter.WriteMarkupLine("[yellow]No bot AI definitions were found. Use --modfile to point at mods.json.[/]");
            return 0;
        }

        var rows = definitions.OrderBy(def => def.Name, StringComparer.OrdinalIgnoreCase)
                              .Select(definition => (IReadOnlyList<string>)[
                                  definition.Name,
                                  definition.Description,
                                  definition.Modules.Count.ToString(CultureInfo.InvariantCulture)
                              ]);
        OutputFormatter.WriteTabularData("Bot definitions", ["Bot", "Description", "Modules"], rows);
        return 0;
    }
}
