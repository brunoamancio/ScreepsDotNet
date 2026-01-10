namespace ScreepsDotNet.Backend.Cli.Commands.Version;

using global::System.Globalization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ScreepsDotNet.Backend.Cli.Formatting;
using ScreepsDotNet.Backend.Cli.Infrastructure;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console.Cli;

internal sealed class VersionCommand(
    IVersionInfoProvider versionInfoProvider,
    ILogger<VersionCommand>? logger = null,
    IHostApplicationLifetime? lifetime = null, ICommandOutputFormatter? outputFormatter = null) : CommandHandler<VersionCommand.Settings>(logger, lifetime, outputFormatter)
{
    public sealed class Settings : FormattableCommandSettings
    {
        [CommandOption("--json")]
        public bool OutputJson { get; init; }
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var info = await versionInfoProvider.GetAsync(cancellationToken).ConfigureAwait(false);

        if (settings.OutputJson) {
            OutputFormatter.WriteJson(info);
            return 0;
        }

        OutputFormatter.WriteKeyValueTable([
                                               ("Protocol", info.Protocol.ToString(CultureInfo.InvariantCulture)),
                                               ("Use Native Auth", info.UseNativeAuth ? "true" : "false"),
                                               ("Users", info.Users.ToString(CultureInfo.InvariantCulture)),
                                               ("Welcome Text", info.ServerData.WelcomeText),
                                               ("Package Version", info.PackageVersion ?? "n/a")
                                           ],
                                           "Version info");
        return 0;
    }
}
