namespace ScreepsDotNet.Driver.Abstractions;

/// <summary>
/// Matches the legacy driver process roles (main, runner, processor, runtime).
/// </summary>
public enum DriverProcessType
{
    Main,
    Runner,
    Processor,
    Runtime
}
