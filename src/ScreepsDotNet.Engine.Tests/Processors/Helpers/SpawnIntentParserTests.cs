namespace ScreepsDotNet.Engine.Tests.Processors.Helpers;

using ScreepsDotNet.Engine.Processors.Helpers;
using ScreepsDotNet.Driver.Contracts;

public sealed class SpawnIntentParserTests
{
    private readonly IBodyAnalysisHelper _bodyHelper = new BodyAnalysisHelper();
    private readonly ISpawnIntentParser _parser;
    private static readonly string[] ValidBody = ["move", "work"];
    private static readonly int[] CreateDirections = [1, 9];
    private static readonly string[] EnergyStructureIds = ["spawn1", "spawn1", "ext1"];
    private static readonly string[] InvalidBody = ["laser"];
    private static readonly int[] MixedDirections = [1, 2, 2, 9];
    private static readonly int[] ExpectedDirections = [1, 2];
    private static readonly int[] SingleDirection = [1];
    private static readonly string[] ExpectedEnergyStructureResult = ["spawn1", "ext1"];

    public SpawnIntentParserTests()
        => _parser = new SpawnIntentParser(_bodyHelper);

    [Fact]
    public void Parse_ReturnsCreateIntent_WhenValid()
    {
        var envelope = new SpawnIntentEnvelope(
            new CreateCreepIntent("Worker1", ValidBody, CreateDirections, EnergyStructureIds),
            null,
            null,
            null,
            false);

        var result = _parser.Parse(envelope);

        Assert.True(result.Success);
        Assert.NotNull(result.CreateIntent);
        Assert.Equal("Worker1", result.CreateIntent!.Name);
        Assert.Equal(2, result.CreateIntent.Body.BodyParts.Count);
        Assert.Equal(SingleDirection, result.CreateIntent.Directions);
        Assert.Equal(ExpectedEnergyStructureResult, result.CreateIntent.EnergyStructureIds);
    }

    [Fact]
    public void Parse_Fails_WhenBodyInvalid()
    {
        var envelope = new SpawnIntentEnvelope(
            new CreateCreepIntent("bad", InvalidBody, null, null),
            null,
            null,
            null,
            false);

        var result = _parser.Parse(envelope);

        Assert.False(result.Success);
        Assert.Contains("Invalid body part", result.Error);
    }

    [Fact]
    public void Parse_ValidatesRenewAndDirections()
    {
        var envelope = new SpawnIntentEnvelope(
            null,
            new RenewCreepIntent("creep1"),
            null,
            new SetSpawnDirectionsIntent(MixedDirections),
            false);

        var result = _parser.Parse(envelope);

        Assert.True(result.Success);
        Assert.Equal("creep1", result.RenewIntent!.TargetId);
        Assert.Equal(ExpectedDirections, result.DirectionsIntent!.Directions);
    }
}
