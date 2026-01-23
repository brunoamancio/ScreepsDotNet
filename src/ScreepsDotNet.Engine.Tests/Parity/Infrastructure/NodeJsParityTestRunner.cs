namespace ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

using System.Diagnostics;
using System.Text;
using System.Text.Json;

/// <summary>
/// Executes Node.js parity test harness and captures JSON output
/// </summary>
public static class NodeJsParityTestRunner
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

        // Run Node.js harness with driver shim (real constants, no native modules)
        Console.WriteLine($"[NodeJsHarnessRunner] MongoDB Connection String: {mongoConnectionString}");
        var driverShimPath = Path.Combine(absoluteHarnessPath, "screeps-modules", "driver-shim.js");
        var startInfo = new ProcessStartInfo
        {
            FileName = "node",
            Arguments = $"test-runner/run-fixture.js \"{absoluteFixturePath}\" --mongo \"{mongoConnectionString}\"",
            WorkingDirectory = absoluteHarnessPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            EnvironmentVariables = {
                // Use driver shim that provides real Screeps constants from @screeps/common
                // without loading native modules (isolated-vm, pathfinder) we don't need
                ["DRIVER_MODULE"] = driverShimPath
            }
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
                $"Connection string: {mongoConnectionString}\n" +
                $"Error: {errorBuilder}");
        }

        var output = outputBuilder.ToString();

        if (string.IsNullOrWhiteSpace(output)) {
            throw new InvalidOperationException("Node.js harness produced no output");
        }

        // Extract JSON from stdout (find complete JSON object)
        // Node.js harness outputs: ... debug messages ... {JSON object} ... more debug messages ...
        // We need to find the JSON object boundaries properly
        var jsonStart = output.IndexOf('{');
        if (jsonStart == -1) {
            throw new InvalidOperationException(
                $"Failed to extract JSON from Node.js output (no JSON object found)\n" +
                $"Output: {output}");
        }

        // Find the matching closing brace by counting braces
        var braceCount = 0;
        var jsonEnd = jsonStart;
        for (var i = jsonStart; i < output.Length; i++) {
            if (output[i] == '{') braceCount++;
            if (output[i] == '}') braceCount--;
            if (braceCount == 0) {
                jsonEnd = i;
                break;
            }
        }

        if (braceCount != 0) {
            throw new InvalidOperationException(
                $"Failed to extract JSON from Node.js output (unmatched braces)\n" +
                $"Output: {output}");
        }

        var jsonOutput = output.Substring(jsonStart, jsonEnd - jsonStart + 1);

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
