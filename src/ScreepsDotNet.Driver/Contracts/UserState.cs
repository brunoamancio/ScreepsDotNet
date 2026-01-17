namespace ScreepsDotNet.Driver.Contracts;

/// <summary>
/// Lightweight projection of user data required by the engine.
/// </summary>
public sealed record UserState(
    string Id,
    string Username,
    double Cpu,
    double Power,
    double Money,
    bool Active,
    double PowerExperimentationTime);
