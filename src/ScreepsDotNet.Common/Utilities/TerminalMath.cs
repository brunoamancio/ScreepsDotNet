namespace ScreepsDotNet.Common.Utilities;

using System;

/// <summary>
/// Shared utility methods for terminal transfers.
/// </summary>
public static class TerminalMath
{
    private const double TransferFalloffFactor = 1.0 / 30.0;

    /// <summary>
    /// Mirrors <c>calcTerminalEnergyCost</c> from the legacy processor:
    /// <c>Math.ceil(amount * (1 - e^(-range / 30)))</c>.
    /// </summary>
    public static int CalculateEnergyCost(int amount, int range)
    {
        if (amount <= 0 || range <= 0)
            return 0;

        var decay = 1 - Math.Exp(-range * TransferFalloffFactor);
        var cost = amount * decay;
        return cost <= 0 ? 0 : (int)Math.Ceiling(cost);
    }
}
