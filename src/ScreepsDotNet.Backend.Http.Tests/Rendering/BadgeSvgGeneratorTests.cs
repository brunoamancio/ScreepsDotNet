using ScreepsDotNet.Backend.Http.Rendering;
using ScreepsDotNet.Backend.Http.Tests.Rendering.Helpers;
using ScreepsDotNet.Backend.Http.Tests.TestSupport;

namespace ScreepsDotNet.Backend.Http.Tests.Rendering;

public sealed class BadgeSvgGeneratorTests
{
    private readonly BadgeSvgGenerator _generator = new();

    [Fact]
    public void GenerateSvg_WithNumericType_ReturnsSvg()
    {
        var badge = new
        {
            color1 = "#112233",
            color2 = "#445566",
            color3 = "#778899",
            type = 1,
            param = 0,
            flip = true
        };

        var svg = _generator.GenerateSvg(badge, includeBorder: false);

        Assert.Contains("clip-path=\"url(#clip)\"", svg);
        Assert.Contains("fill=\"#112233\"", svg);
        Assert.Contains("rotate(0", svg);
        Assert.Contains("path d=\"M 50", svg);
    }

    [Fact]
    public void GenerateSvg_WithBorder_RendersCircle()
    {
        var badge = new
        {
            color1 = "#000000",
            color2 = "#111111",
            color3 = "#222222",
            type = 2,
            param = 50,
            flip = false
        };

        var svg = _generator.GenerateSvg(badge, includeBorder: true);

        Assert.Contains("stroke-width=\"5\"", svg);
        Assert.Contains("r=\"47.5\"", svg);
    }

    [Fact]
    public void GenerateSvg_WithCustomPaths_UsesPaths()
    {
        var badge = new
        {
            color1 = "#123456",
            color2 = "#654321",
            color3 = "#abcdef",
            type = new
            {
                path1 = "M 0 0 L 50 50 Z",
                path2 = "M 50 50 L 100 100 Z"
            },
            param = 0
        };

        var svg = _generator.GenerateSvg(badge, includeBorder: false);

        Assert.Contains("M 0 0 L 50 50 Z", svg);
        Assert.Contains("M 50 50 L 100 100 Z", svg);
    }

    [Fact]
    public void GenerateSvg_InvalidData_ReturnsEmptySvg()
    {
        var svg = _generator.GenerateSvg(new { }, includeBorder: false);

        Assert.Equal("<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>", svg);
    }

    [Fact]
    public void Export_BadgeSamples_ToDocsMarkdown()
    {
        var projectRoot = RepositoryPathHelper.FindRepositoryRoot();
        var docsDir = Path.Combine(projectRoot, "docs", "badges");
        Directory.CreateDirectory(docsDir);

        var numericSamples = BadgeSampleFactory.CreateNumericSamples(24);
        var customSample = BadgeSampleFactory.CreateCustomSample();
        var markdown = BadgeGalleryMarkdownBuilder.Build(_generator, numericSamples, customSample);

        var outputFile = Path.Combine(docsDir, "BadgeGallery.md");
        File.WriteAllText(outputFile, markdown);
        Assert.True(File.Exists(outputFile));
    }
}
