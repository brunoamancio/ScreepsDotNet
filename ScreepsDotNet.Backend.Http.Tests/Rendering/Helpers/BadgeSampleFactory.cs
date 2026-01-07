using System.Globalization;
using System.Text;

namespace ScreepsDotNet.Backend.Http.Tests.Rendering.Helpers;

internal static class BadgeSampleFactory
{
    private const string NumericLabelFormatText = "Type {0:00}";
    private const string CustomSampleLabel = "Custom";
    private const string CustomOuterPath = "M 10 10 L 90 10 L 90 90 L 10 90 Z";
    private const string CustomInnerPath = "M 30 30 L 70 30 L 70 70 L 30 70 Z";
    private static readonly CompositeFormat NumericLabelComposite = CompositeFormat.Parse(NumericLabelFormatText);

    public static IReadOnlyList<BadgeSample> CreateNumericSamples(int maxType)
    {
        var samples = new List<BadgeSample>(maxType);

        for (var type = 1; type <= maxType; type++) {
            var payload = new BadgePayload
            {
                color1 = type % 30,
                color2 = (type + 5) % 30,
                color3 = (type + 10) % 30,
                type = type,
                param = (type % 50) - 25,
                flip = type % 3 == 0
            };

            var label = string.Format(CultureInfo.InvariantCulture, NumericLabelComposite, type);
            samples.Add(new BadgeSample(label, payload, IncludeBorder: true));
        }

        return samples;
    }

    public static BadgeSample CreateCustomSample()
    {
        var payload = new BadgePayload
        {
            color1 = "#123456",
            color2 = "#abcdef",
            color3 = "#fedcba",
            type = new
            {
                path1 = CustomOuterPath,
                path2 = CustomInnerPath
            },
            param = 0
        };

        return new BadgeSample(CustomSampleLabel, payload, IncludeBorder: false);
    }
}

internal readonly record struct BadgeSample(string Label, BadgePayload Payload, bool IncludeBorder);

internal sealed class BadgePayload
{
    public object? color1 { get; init; }
    public object? color2 { get; init; }
    public object? color3 { get; init; }
    public object? type { get; init; }
    public double param { get; init; }
    public bool flip { get; init; }
}
