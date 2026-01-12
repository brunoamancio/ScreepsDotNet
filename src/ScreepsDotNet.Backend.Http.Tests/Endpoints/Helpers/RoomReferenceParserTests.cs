namespace ScreepsDotNet.Backend.Http.Tests.Endpoints.Helpers;

using ScreepsDotNet.Backend.Core.Parsing;
using Xunit;

public sealed class RoomReferenceParserTests
{
    private static readonly string[] DedupSample = ["shard1/W20N20", "W20N20", "w21n21"];
    private static readonly string[] InvalidSample = ["valid", "/invalid", "E10Q5", "shard#/W10N5"];

    [Theory]
    [InlineData("W10N5", null, "W10N5", null)]
    [InlineData("shard2/W11N6", null, "W11N6", "shard2")]
    [InlineData(" shard3 /W12N7 ", null, "W12N7", "shard3")]
    [InlineData("shard3/W13N8", "overrideShard", "W13N8", "overrideShard")]
    [InlineData("W14N9", "overrideShard", "W14N9", "overrideShard")]
    [InlineData("shard4/w15s9", null, "W15S9", "shard4")]
    public void TryParse_ValidInputs_ReturnReference(string input, string? overrideShard, string expectedRoom, string? expectedShard)
    {
        var success = RoomReferenceParser.TryParse(input, overrideShard, out var reference);

        Assert.True(success);
        Assert.NotNull(reference);
        Assert.Equal(expectedRoom, reference!.RoomName);
        Assert.Equal(expectedShard, reference.ShardName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/W10N5")]
    [InlineData("shard/")]
    [InlineData("X10N5")]
    [InlineData("W10Q5")]
    [InlineData("shard!/W10N5")]
    public void TryParse_InvalidInputs_ReturnsFalse(string input)
    {
        var success = RoomReferenceParser.TryParse(input, null, out var reference);

        Assert.False(success);
        Assert.Null(reference);
    }

    [Fact]
    public void TryParseRooms_DeduplicatesAndRespectsShardOverrides()
    {
        var result = RoomReferenceParser.TryParseRooms(DedupSample, "override", out var references);

        Assert.True(result);
        Assert.Equal(2, references.Count);
        Assert.All(references, reference => Assert.Equal("override", reference.ShardName));
    }

    [Fact]
    public void TryParseRooms_InvalidEntry_Fails()
    {
        var result = RoomReferenceParser.TryParseRooms(InvalidSample, null, out var references);

        Assert.False(result);
        Assert.Empty(references);
    }
}
