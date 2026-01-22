namespace ScreepsDotNet.Engine.Tests.Parity.Integration;

using System.Diagnostics;
using System.Text.RegularExpressions;

/// <summary>
/// Test fixture that ensures all prerequisites for parity testing are met.
/// Automatically checks Node.js (nvm-aware), Docker, official Screeps repos, and npm dependencies.
///
/// Node.js Compatibility: 10.13.0 to 12.x ONLY
/// - Screeps engine was built for Node 10.x and does NOT work with Node 13+
/// - Recommended: Node 12.22.12 (last LTS version of Node 12)
///
/// Node.js Detection Strategy:
/// 1. Check if nvm is available (Linux/Mac/Windows)
/// 2. If nvm exists: Find highest installed Node version in range [10.13.0, 12.x], activate via 'nvm use'
/// 3. If nvm not exists: Check 'node --version' directly and validate range
/// 4. Fail with helpful error if Node.js not found, too old, or too new
/// </summary>
public sealed partial class ParityTestPrerequisites : IAsyncLifetime
{
    // Screeps engine compatibility: Node 10.13.0 to 12.x ONLY
    // Screeps was built for Node 10.x and does NOT work with Node 13+
    private const string MinNodeVersion = "10.13.0";
    private const string MaxNodeVersion = "12.999.999"; // Accept any Node 12.x, reject 13+
    private const string RecommendedVersion = "12.22.12"; // LTS version for testing
    private const string HarnessRelativePath = "tools/parity-harness/engine";

    [GeneratedRegex(@"v?(\d+\.\d+\.\d+)")]
    private static partial Regex NodeVersionRegex();

    public string HarnessDirectory { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        // Find repo root
        var repoRoot = FindRepoRoot();
        HarnessDirectory = Path.Combine(repoRoot, HarnessRelativePath);

        // Check prerequisites in order
        await EnsureNodeInstalled();
        await EnsureDockerRunning();
        await EnsureScreepsReposCloned();
        await EnsureHarnessDependencies();
    }

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;

    private static string FindRepoRoot()
    {
        var assemblyLocation = AppContext.BaseDirectory;
        var current = new DirectoryInfo(assemblyLocation);

        while (current is not null) {
            var harnessPath = Path.Combine(current.FullName, HarnessRelativePath);
            if (Directory.Exists(harnessPath))
                return current.FullName;

            current = current.Parent;
        }

        throw new InvalidOperationException(
            $"Could not find repository root. Expected '{HarnessRelativePath}' to exist.");
    }

    private static async Task EnsureNodeInstalled()
    {
        // Check if nvm is available first
        var nvmAvailable = await IsNvmAvailable();

        if (nvmAvailable) {
            await EnsureNodeViaNvm();
        }
        else {
            await EnsureNodeViaDirectCheck();
        }
    }

    private static async Task<bool> IsNvmAvailable()
    {
        try {
            var isWindows = OperatingSystem.IsWindows();

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = isWindows ? "nvm" : "bash",
                    Arguments = isWindows ? "version" : "-c \"source ~/.nvm/nvm.sh 2>/dev/null || source ~/.config/nvm/nvm.sh 2>/dev/null; command -v nvm\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return process.ExitCode == 0;
        }
        catch {
            return false;
        }
    }

    private static async Task EnsureNodeViaNvm()
    {
        var isWindows = OperatingSystem.IsWindows();

        // Get list of installed Node versions from nvm
        var listProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = isWindows ? "nvm" : "bash",
                Arguments = isWindows ? "list" : "-c \"source ~/.nvm/nvm.sh 2>/dev/null || source ~/.config/nvm/nvm.sh 2>/dev/null; nvm list\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        listProcess.Start();
        var nvmListOutput = await listProcess.StandardOutput.ReadToEndAsync();
        await listProcess.WaitForExitAsync();

        if (listProcess.ExitCode != 0) {
            throw new InvalidOperationException(
                "nvm is installed but failed to list Node.js versions.\n" +
                $"Try running: {(isWindows ? "nvm list" : "nvm list")}");
        }

        // Parse versions and find one within compatible range
        var minVersion = Version.Parse(MinNodeVersion);
        var maxVersion = Version.Parse(MaxNodeVersion);
        Version? suitableVersion = null;
        var suitableVersionString = string.Empty;

        foreach (var line in nvmListOutput.Split('\n')) {
            var versionMatch = NodeVersionRegex().Match(line);
            if (versionMatch.Success) {
                var versionString = versionMatch.Groups[1].Value;
                var version = Version.Parse(versionString);

                // Find highest version within compatible range [min, max]
                if (version >= minVersion && version <= maxVersion && (suitableVersion is null || version > suitableVersion)) {
                    suitableVersion = version;
                    suitableVersionString = versionString;
                }
            }
        }

        if (suitableVersion is null) {
            throw new InvalidOperationException(
                $"nvm is installed but no compatible Node.js version found.\n" +
                $"Required: Node.js {MinNodeVersion} to {maxVersion.Major}.x (Screeps engine does NOT work with Node 13+)\n" +
                $"Install with: {(isWindows ? $"nvm install {RecommendedVersion}" : $"nvm install {RecommendedVersion}")} (recommended Node 12 LTS)\n" +
                $"Available versions:\n{nvmListOutput.Trim()}");
        }

        // Activate the suitable version by modifying PATH
        Console.WriteLine($"Using Node.js {suitableVersionString} via nvm...");

        // Construct path to nvm Node.js bin directory
        var nvmNodePath = isWindows
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Roaming", "nvm", $"v{suitableVersionString}")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nvm", "versions", "node", $"v{suitableVersionString}", "bin");

        if (!Directory.Exists(nvmNodePath)) {
            throw new InvalidOperationException(
                $"nvm Node.js directory not found: {nvmNodePath}\n" +
                $"Expected structure:\n" +
                $"  Linux/Mac: ~/.nvm/versions/node/v{suitableVersionString}/bin\n" +
                $"  Windows: %USERPROFILE%\\AppData\\Roaming\\nvm\\v{suitableVersionString}");
        }

        // Prepend nvm Node.js path to PATH environment variable
        var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var newPath = $"{nvmNodePath}{Path.PathSeparator}{currentPath}";
        Environment.SetEnvironmentVariable("PATH", newPath);

        Console.WriteLine($"Set PATH to use Node.js from: {nvmNodePath}");

        // Verify node is now available with correct version
        await EnsureNodeViaDirectCheck();
    }

    private static async Task EnsureNodeViaDirectCheck()
    {
        try {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "node",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new InvalidOperationException("Node.js not found or not in PATH.");

            var versionMatch = NodeVersionRegex().Match(output);
            if (!versionMatch.Success)
                throw new InvalidOperationException($"Could not parse Node.js version from: {output}");

            var installedVersion = Version.Parse(versionMatch.Groups[1].Value);
            var minVersion = Version.Parse(MinNodeVersion);
            var maxVersion = Version.Parse(MaxNodeVersion);

            if (installedVersion < minVersion) {
                throw new InvalidOperationException(
                    $"Node.js version {installedVersion} is too old.\n" +
                    $"Required: Node.js {MinNodeVersion} to {maxVersion.Major}.x\n" +
                    $"Download Node 12.x LTS from: https://nodejs.org/dist/latest-v12.x/\n" +
                    $"Or install via nvm: nvm install {RecommendedVersion}");
            }

            if (installedVersion > maxVersion) {
                throw new InvalidOperationException(
                    $"Node.js version {installedVersion} is too new for Screeps engine (does NOT work with Node 13+).\n" +
                    $"Required: Node.js {MinNodeVersion} to {maxVersion.Major}.x\n" +
                    $"Install compatible version: nvm install {RecommendedVersion} && nvm use {RecommendedVersion}\n" +
                    $"Or download Node 12.x LTS from: https://nodejs.org/dist/latest-v12.x/");
            }

            Console.WriteLine($"Node.js {installedVersion} is available and compatible.");
        }
        catch (Exception ex) when (ex is not InvalidOperationException) {
            throw new InvalidOperationException(
                $"Node.js not found. Please install Node.js {MinNodeVersion} to {Version.Parse(MaxNodeVersion).Major}.x\n" +
                $"Download Node 12.x LTS from: https://nodejs.org/dist/latest-v12.x/\n" +
                $"Or install via nvm: nvm install {RecommendedVersion}\n" +
                $"Error: {ex.Message}", ex);
        }
    }

    private static async Task EnsureDockerRunning()
    {
        try {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = "info",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0) {
                throw new InvalidOperationException(
                    "Docker is not running. Please start Docker Desktop or dockerd.");
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException) {
            throw new InvalidOperationException(
                "Docker not found or not running. Parity tests require Docker.\n" +
                "Install from: https://www.docker.com/get-started\n" +
                $"Error: {ex.Message}", ex);
        }
    }

    private async Task EnsureScreepsReposCloned()
    {
        var reposPath = Path.Combine(HarnessDirectory, "screeps-modules");

        if (Directory.Exists(reposPath) && Directory.EnumerateDirectories(reposPath).Any())
            return; // Repos already cloned

        Console.WriteLine("Cloning official Screeps repositories... (this may take 30-60 seconds)");

        var scriptsPath = Path.Combine(HarnessDirectory, "scripts");
        var isWindows = OperatingSystem.IsWindows();
        var scriptName = isWindows ? "clone-repos.ps1" : "clone-repos.sh";
        var scriptPath = Path.Combine(scriptsPath, scriptName);

        if (!File.Exists(scriptPath)) {
            throw new InvalidOperationException(
                $"Clone script not found: {scriptPath}\n" +
                $"Expected location: {scriptsPath}");
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = isWindows ? "pwsh" : "bash",
                Arguments = scriptPath,
                WorkingDirectory = HarnessDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = false // Show output for long-running operation
            }
        };

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0) {
            throw new InvalidOperationException(
                $"Failed to clone official Screeps repositories.\n" +
                $"Script: {scriptPath}\n" +
                $"Exit code: {process.ExitCode}\n" +
                $"Output: {output}\n" +
                $"Error: {error}");
        }

        Console.WriteLine("Official Screeps repositories cloned successfully.");
    }

    private async Task EnsureHarnessDependencies()
    {
        var nodeModulesPath = Path.Combine(HarnessDirectory, "node_modules");

        if (Directory.Exists(nodeModulesPath))
            return; // Dependencies already installed

        Console.WriteLine("Installing Node.js harness dependencies... (this may take 20-30 seconds)");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "npm",
                Arguments = "install",
                WorkingDirectory = HarnessDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = false // Show output for long-running operation
            }
        };

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0) {
            throw new InvalidOperationException(
                $"Failed to install Node.js dependencies.\n" +
                $"Working directory: {HarnessDirectory}\n" +
                $"Exit code: {process.ExitCode}\n" +
                $"Output: {output}\n" +
                $"Error: {error}");
        }

        Console.WriteLine("Node.js harness dependencies installed successfully.");
    }
}
