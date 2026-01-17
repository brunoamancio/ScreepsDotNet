using ScreepsDotNet.Common.Utilities;

namespace ScreepsDotNet.Engine.Tests.Utilities;

public sealed class TerminalMathTests
{
    [Fact]
    public void CalculateEnergyCost_MatchesLegacyFormula()
    {
        Assert.Equal(0, TerminalMath.CalculateEnergyCost(500, 0));
        Assert.Equal(142, TerminalMath.CalculateEnergyCost(500, 10));
        Assert.Equal(244, TerminalMath.CalculateEnergyCost(500, 20));
    }

    [Fact]
    public void StoreMath_SumsValues()
    {
        var store = new Dictionary<string, int>
        {
            ["energy"] = 100,
            ["H"] = 25
        };

        Assert.Equal(125, StoreMath.Sum(store));
        Assert.Equal(0, StoreMath.Sum(null));
    }
}
