namespace ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

using System.Diagnostics;
using System.Text;
using System.Text.Json;

/// <summary>
/// Executes Node.js parity test harness and captures JSON output
/// </summary>
public static class NodeJsHarnessRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    public static async Task<JsonDocument> RunFixtureAsync(string fixturePath, string harnessDirectory, string mongoConnectionString, CancellationToken cancellationToken = default)
    {
        var absoluteHarnessPath = Path.GetFullPath(harnessDirectory);
        var absoluteFixturePath = Path.GetFullPath(fixturePath);

        // Check if harness exists
        if (!Directory.Exists(absoluteHarnessPath)) {
            throw new InvalidOperationException(
                $"Node.js harness not found at: {absoluteHarnessPath}\n" +
                "Run 'cd tools/parity-harness/engine && npm install' first");
        }

        // Run Node.js harness
        var startInfo = new ProcessStartInfo
        {
            FileName = "node",
            Arguments = $"test-runner/run-fixture.js \"{absoluteFixturePath}\" --mongo \"{mongoConnectionString}\"",
            WorkingDirectory = absoluteHarnessPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process();
        process.StartInfo = startInfo;
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) => {
            if (e.Data is not null) {
                outputBuilder.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) => {
            if (e.Data is not null) {
                errorBuilder.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0) {
            throw new InvalidOperationException(
                $"Node.js harness failed with exit code {process.ExitCode}\n" +
                $"Error: {errorBuilder}");
        }

        var output = outputBuilder.ToString();
        if (string.IsNullOrWhiteSpace(output)) {
            throw new InvalidOperationException("Node.js harness produced no output");
        }

        // Extract JSON from stdout (between first '{' and last '}')
        // Node.js harness outputs debug messages mixed with JSON
        var firstBrace = output.IndexOf('{');
        var lastBrace = output.LastIndexOf('}');

        if (firstBrace == -1 || lastBrace == -1 || firstBrace >= lastBrace) {
            throw new InvalidOperationException(
                $"Failed to extract JSON from Node.js output (no JSON object found)\n" +
                $"Output: {output}");
        }

        var jsonOutput = output.Substring(firstBrace, lastBrace - firstBrace + 1);

        // Parse and return as JsonDocument for parity comparison
        try {
            return JsonDocument.Parse(jsonOutput);
        }
        catch (JsonException ex) {
            throw new InvalidOperationException(
                $"Failed to parse Node.js output as JSON: {ex.Message}\n" +
                $"Extracted JSON: {jsonOutput}",
                ex);
        }
    }
}
