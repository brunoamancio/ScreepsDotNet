namespace ScreepsDotNet.Engine.Tests.Parity.Comparison;

/// <summary>
/// Represents a single divergence between Node.js and .NET engine outputs
/// </summary>
public sealed record ParityDivergence(
    string Path,
    object? NodeValue,
    object? DotNetValue,
    string Message,
    DivergenceCategory Category
);

/// <summary>
/// Categories of parity divergences for grouping/reporting
/// </summary>
public enum DivergenceCategory
{
    Mutation,
    Stats,
    ActionLog,
    FinalState,
    Other
}

/// <summary>
/// Result of parity comparison between Node.js and .NET outputs
/// </summary>
public sealed record ParityComparisonResult(
    IReadOnlyList<ParityDivergence> Divergences,
    bool HasDivergences
)
{
    public ParityComparisonResult(IReadOnlyList<ParityDivergence> divergences)
        : this(divergences, divergences.Count > 0)
    {
    }

    public static ParityComparisonResult Success()
        => new([], false);
}
