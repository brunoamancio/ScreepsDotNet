namespace ScreepsDotNet.Engine.Tests.Parity.Comparison;

using System.Text;

/// <summary>
/// Formats parity divergences into human-readable reports
/// </summary>
public static class DivergenceReporter
{
    public static string FormatReport(ParityComparisonResult result, string fixtureName)
    {
        if (!result.HasDivergences)
        {
            return $"✅ Parity Test Passed: {fixtureName}\n\nNo divergences found.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"❌ Parity Test Failed: {fixtureName}");
        sb.AppendLine();
        sb.AppendLine($"Divergences ({result.Divergences.Count}):");
        sb.AppendLine();

        var groupedDivergences = result.Divergences
            .GroupBy(d => d.Category)
            .OrderBy(g => g.Key);

        var index = 1;
        foreach (var group in groupedDivergences)
        {
            sb.AppendLine($"--- {group.Key} ---");
            sb.AppendLine();

            foreach (var divergence in group)
            {
                sb.AppendLine($"{index}. {divergence.Path}");
                sb.AppendLine($"   Node.js: {FormatValue(divergence.NodeValue)}");
                sb.AppendLine($"   .NET:    {FormatValue(divergence.DotNetValue)}");
                sb.AppendLine($"   Message: {divergence.Message}");
                sb.AppendLine();
                index++;
            }
        }

        return sb.ToString();
    }

    public static string FormatSummary(ParityComparisonResult result)
    {
        if (!result.HasDivergences)
        {
            return "✅ All fields match";
        }

        var categoryGroups = result.Divergences
            .GroupBy(d => d.Category)
            .ToDictionary(g => g.Key, g => g.Count());

        var parts = new List<string>();
        foreach (var (category, count) in categoryGroups.OrderBy(kvp => kvp.Key))
        {
            parts.Add($"{category}: {count}");
        }

        return $"❌ {result.Divergences.Count} divergence(s) ({string.Join(", ", parts)})";
    }

    private static string FormatValue(object? value)
    {
        if (value is null)
        {
            return "<null>";
        }

        if (value is string str)
        {
            return $"\"{str}\"";
        }

        if (value is int or long or double or float)
        {
            return value.ToString()!;
        }

        if (value is bool boolean)
        {
            return boolean ? "true" : "false";
        }

        return value.ToString() ?? "<unknown>";
    }
}
