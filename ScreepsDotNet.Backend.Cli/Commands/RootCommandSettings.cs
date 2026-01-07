namespace ScreepsDotNet.Backend.Cli.Commands;

using System;
using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class RootCommandSettings : CommandSettings
{
    [CommandOption("--db|--storage|--storage-backend <BACKEND>")]
    [Description("Selects the storage backend (default: mongodb).")]
    [DefaultValue("mongodb")]
    public string StorageBackend { get; init; } = "mongodb";

    [CommandOption("--connection-string|--mongo <URI>")]
    [Description("Overrides the MongoDB connection string.")]
    public string? ConnectionString { get; init; }

    [CommandOption("--cli_host|--cli-host <HOST>")]
    [Description("CLI server host binding (legacy compatibility).")]
    public string? CliHost { get; init; }

    [CommandOption("--cli_port|--cli-port <PORT>")]
    [Description("CLI server port (legacy compatibility).")]
    public int? CliPort { get; init; }

    [CommandOption("--host <HOST>")]
    [Description("HTTP server host binding (legacy compatibility).")]
    public string? Host { get; init; }

    [CommandOption("--port <PORT>")]
    [Description("HTTP server port (legacy compatibility).")]
    public int? Port { get; init; }

    [CommandOption("--password <PASSWORD>")]
    [Description("Server password required for client authentication.")]
    public string? Password { get; init; }

    [CommandOption("--steam_api_key|--steam-api-key <KEY>")]
    [Description("Steam Web API key for headless authentication.")]
    public string? SteamApiKey { get; init; }

    [CommandOption("--runners_cnt|--runners-cnt <COUNT>")]
    [Description("Number of runner worker processes (legacy compatibility).")]
    public int? RunnerCount { get; init; }

    [CommandOption("--processors_cnt|--processors-cnt <COUNT>")]
    [Description("Number of processor worker processes (legacy compatibility).")]
    public int? ProcessorCount { get; init; }

    [CommandOption("--assetdir|--asset-dir <PATH>")]
    [Description("Path to static asset directory.")]
    public string? AssetDirectory { get; init; }

    [CommandOption("--logdir|--log-dir <PATH>")]
    [Description("Directory where log files should be written.")]
    public string? LogDirectory { get; init; }

    [CommandOption("--modfile|--mod-file <PATH>")]
    [Description("Path to the mods manifest file.")]
    public string? ModFile { get; init; }

    public override ValidationResult Validate()
    {
        if (!string.Equals(StorageBackend, "mongodb", StringComparison.OrdinalIgnoreCase))
            return ValidationResult.Error("Only the 'mongodb' storage backend is supported.");

        if (CliPort is <= 0)
            return ValidationResult.Error("CLI port must be a positive integer.");

        if (Port is <= 0)
            return ValidationResult.Error("HTTP port must be a positive integer.");

        if (RunnerCount is <= 0)
            return ValidationResult.Error("Runner count must be a positive integer.");

        if (ProcessorCount is <= 0)
            return ValidationResult.Error("Processor count must be a positive integer.");

        return ValidationResult.Success();
    }
}
