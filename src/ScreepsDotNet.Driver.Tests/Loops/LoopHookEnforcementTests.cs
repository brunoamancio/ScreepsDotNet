using System.Text;
using ScreepsDotNet.Driver.Abstractions.History;
using ScreepsDotNet.Driver.Abstractions.Notifications;
using ScreepsDotNet.Driver.Abstractions.Runtime;
using ScreepsDotNet.Driver.Services.History;
using ScreepsDotNet.Driver.Services.Loops;
using ScreepsDotNet.Driver.Services.Notifications;

namespace ScreepsDotNet.Driver.Tests.Loops;

public sealed class LoopHookEnforcementTests
{
    private static readonly string[] BannedTokens =
    [
        nameof(IHistoryService),
        nameof(HistoryService),
        nameof(INotificationService),
        nameof(NotificationService),
        nameof(IRuntimeTelemetrySink)
    ];

    [Fact]
    public void LoopServicesDoNotDependOnProhibitedServices()
    {
        var loopDirectory = Path.Combine(GetRepoRoot(), "src", "ScreepsDotNet.Driver", "Services", "Loops");
        Assert.True(Directory.Exists(loopDirectory), $"Loops directory not found: {loopDirectory}");

        var violations = new List<string>();
        var exceptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            $"{nameof(DriverLoopHooks)}.cs",
            $"{nameof(RoomsDoneBroadcaster)}.cs"
        };

        foreach (var file in Directory.GetFiles(loopDirectory, "*.cs", SearchOption.TopDirectoryOnly))
        {
            if (exceptions.Contains(Path.GetFileName(file)))
                continue;

            var content = File.ReadAllText(file);
            foreach (var token in BannedTokens)
            {
                if (content.Contains(token, StringComparison.Ordinal))
                    violations.Add($"{Path.GetFileName(file)} references {token}");
            }
        }

        if (violations.Count > 0)
        {
            var builder = new StringBuilder()
                .AppendLine("Loop services should rely on IDriverLoopHooks instead of directly injecting history/notification/telemetry services.")
                .AppendLine("Violations:")
                .AppendLine(string.Join(Environment.NewLine, violations));
            Assert.Fail(builder.ToString());
        }
    }

    private static string GetRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (Directory.Exists(Path.Combine(dir, "src")) && File.Exists(Path.Combine(dir, "README.md")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root.");
    }
}
