namespace ScreepsDotNet.Backend.Cli.Commands.Stronghold;

using global::System;
using global::System.Globalization;
using global::System.Linq;
using global::System.Text.Json;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class StrongholdTemplatesCommand(IStrongholdTemplateProvider templateProvider) : AsyncCommand<StrongholdTemplatesCommand.Settings>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--json")]
        public bool OutputJson { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var templates = await templateProvider.GetTemplatesAsync(cancellationToken).ConfigureAwait(false);
        var depositTypes = await templateProvider.GetDepositTypesAsync(cancellationToken).ConfigureAwait(false);

        if (settings.OutputJson) {
            var payload = new
            {
                Templates = templates.Select(template => new
                {
                    template.Name,
                    template.Description,
                    template.RewardLevel,
                    StructureCount = template.Structures.Count
                }),
                DepositTypes = depositTypes
            };
            AnsiConsole.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
            return 0;
        }

        if (templates.Count == 0) {
            AnsiConsole.MarkupLine("[yellow]No stronghold templates are available.[/]");
            return 0;
        }

        var table = new Table().AddColumn("Template").AddColumn("Reward").AddColumn("Structures");
        foreach (var template in templates.OrderBy(t => t.RewardLevel).ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase))
            table.AddRow(template.Name, template.RewardLevel.ToString(CultureInfo.InvariantCulture), template.Structures.Count.ToString(CultureInfo.InvariantCulture));

        AnsiConsole.Write(table);

        if (depositTypes.Count > 0) {
            AnsiConsole.MarkupLine($"\nDeposit types: [cyan]{string.Join(", ", depositTypes)}[/]");
        }

        return 0;
    }
}
