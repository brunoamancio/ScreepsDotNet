namespace ScreepsDotNet.Engine.Tests.Processors.Helpers;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Engine.Processors.Helpers;

public sealed class BodyAnalysisHelperTests
{
    private readonly IBodyAnalysisHelper _helper = new BodyAnalysisHelper();
    private static readonly BodyPartType[] ValidBody = [BodyPartType.Move, BodyPartType.Work, BodyPartType.Carry];

    [Fact]
    public void Analyze_ReturnsMetrics_ForValidBody()
    {
        var result = _helper.Analyze(ValidBody);

        Assert.True(result.Success);
        Assert.Equal(ValidBody.Length * 3, result.SpawnTime);
        Assert.Equal(ValidBody.Length * 100, result.TotalHits);
        Assert.Equal(50 + 100 + 50, result.TotalEnergyCost);
        Assert.Equal(50, result.CarryCapacity);
        Assert.Equal(1, result.PartCounts[BodyPartType.Move]);
    }

    [Fact]
    public void Analyze_Fails_WhenBodyEmpty()
    {
        var result = _helper.Analyze([]);

        Assert.False(result.Success);
        Assert.Contains("required", result.Error);
    }

    [Fact]
    public void Analyze_Fails_WhenBodyTooLarge()
    {
        var parts = Enumerable.Repeat(BodyPartType.Move, ScreepsGameConstants.MaxCreepBodyParts + 1).ToArray();

        var result = _helper.Analyze(parts);

        Assert.False(result.Success);
        Assert.Contains("exceeds", result.Error);
    }
}
