namespace ScreepsDotNet.Backend.Cli.Commands.Version;

using global::System.Text.Json;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class VersionCommand(IVersionInfoProvider versionInfoProvider) : AsyncCommand<VersionCommand.Settings>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--json")]
        public bool OutputJson { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var info = await versionInfoProvider.GetAsync(cancellationToken).ConfigureAwait(false);

        if (settings.OutputJson) {
            var payload = JsonSerializer.Serialize(info, JsonOptions);
            AnsiConsole.WriteLine(payload);
            return 0;
        }

        var table = new Table().AddColumn("Field").AddColumn("Value");
        table.AddRow("Protocol", info.Protocol.ToString());
        table.AddRow("Use Native Auth", info.UseNativeAuth.ToString());
        table.AddRow("Users", info.Users.ToString());
        table.AddRow("Welcome Text", info.ServerData.WelcomeText);
        table.AddRow("Package Version", info.PackageVersion ?? "n/a");
        AnsiConsole.Write(table);
        return 0;
    }
}
