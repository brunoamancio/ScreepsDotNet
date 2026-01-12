using System.Text.Json.Nodes;
using ScreepsDotNet.Driver.Services.History;

namespace ScreepsDotNet.Driver.Tests.History;

public sealed class HistoryDiffBuilderTests
{
    [Fact]
    public void BuildChunk_ReturnsNullWhenBaseMissing()
    {
        var ticks = new Dictionary<int, JsonNode?>()
        {
            [11] = JsonNode.Parse("""{"hits":100}""")
        };

        var result = HistoryDiffBuilder.BuildChunk(ticks, 10);

        Assert.Null(result);
    }

    [Fact]
    public void BuildChunk_ComputesDiffs()
    {
        var baseDoc = JsonNode.Parse("""{"hits":100,"pos":{"x":10,"y":20}}""");
        var nextDoc = JsonNode.Parse("""{"hits":80,"pos":{"x":10,"y":20}}""");
        var thirdDoc = JsonNode.Parse("""{"hits":80,"pos":{"x":11,"y":20}}""");

        var ticks = new Dictionary<int, JsonNode?>()
        {
            [50] = baseDoc,
            [51] = nextDoc,
            [52] = thirdDoc
        };

        var result = HistoryDiffBuilder.BuildChunk(ticks, 50);

        Assert.NotNull(result);
        Assert.Equal(3, result!.Count);
        Assert.True(JsonNode.DeepEquals(baseDoc, result[50]));

        var diff51 = result[51]!.AsObject();
        Assert.Equal(80, diff51["hits"]!.GetValue<int>());

        var diff52 = result[52]!.AsObject();
        Assert.Equal(11, diff52["pos"]!["x"]!.GetValue<int>());
    }
}
