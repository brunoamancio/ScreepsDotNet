using System.Globalization;
using System.Text;
using System.Text.Json;
using ScreepsDotNet.Backend.Core.Services;

namespace ScreepsDotNet.Backend.Http.Rendering;

internal sealed class BadgeSvgGenerator : IBadgeSvgGenerator
{
    private const string EmptySvg = "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>";
    private const string DefaultColor = "#000000";
    private const double DefaultClipRadius = 52;
    private const double BorderClipRadius = 48;
    private const double BorderCircleRadius = 47.5;
    private const string ClipId = "clip";

    private static readonly IReadOnlyList<string> Palette = BuildPalette();
    private static readonly IReadOnlyDictionary<int, BadgePathDefinition> PathDefinitions = BuildPathDefinitions();

    public string GenerateSvg(object? badgeData, bool includeBorder)
    {
        if (!TryCreateDescriptor(badgeData, out var descriptor))
            return EmptySvg;

        var color1 = ResolveColor(descriptor.Color1);
        var color2 = ResolveColor(descriptor.Color2);
        var color3 = ResolveColor(descriptor.Color3);

        BadgePathResult pathResult;
        double rotation = 0;

        if (descriptor.NumericType is { } type)
        {
            if (!PathDefinitions.TryGetValue(type, out var definition))
                return EmptySvg;

            pathResult = definition.Calculator(descriptor.Param);
            if (descriptor.Flip && definition.FlipRotationDegrees.HasValue)
                rotation = definition.FlipRotationDegrees.Value;
        }
        else if (!string.IsNullOrWhiteSpace(descriptor.CustomPath1))
            pathResult = new BadgePathResult(descriptor.CustomPath1!, descriptor.CustomPath2);
        else
            return EmptySvg;

        if (string.IsNullOrWhiteSpace(pathResult.Path1))
            return EmptySvg;

        return BuildSvg(pathResult, color1, color2, color3, rotation, includeBorder);
    }

    private static string BuildSvg(BadgePathResult paths, string color1, string color2, string color3, double rotation, bool includeBorder)
    {
        var builder = new StringBuilder();
        builder.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"128\" height=\"128\" viewBox=\"0 0 100 100\" shape-rendering=\"geometricPrecision\">");
        var clipRadius = includeBorder ? BorderClipRadius : DefaultClipRadius;
        builder.Append("<defs><clipPath id=\"").Append(ClipId).Append("\"><circle cx=\"50\" cy=\"50\" r=\"")
               .Append(ToString(clipRadius)).Append("\" /></clipPath></defs>");
        builder.Append("<g transform=\"rotate(").Append(ToString(rotation)).Append(" 50 50)\">");
        builder.Append("<rect x=\"0\" y=\"0\" width=\"100\" height=\"100\" fill=\"")
               .Append(color1).Append("\" clip-path=\"url(#").Append(ClipId).Append(")\"/>");
        builder.Append("<path d=\"").Append(paths.Path1).Append("\" fill=\"").Append(color2)
               .Append("\" clip-path=\"url(#").Append(ClipId).Append(")\"/>");

        if (!string.IsNullOrWhiteSpace(paths.Path2))
        {
            builder.Append("<path d=\"").Append(paths.Path2).Append("\" fill=\"").Append(color3)
                   .Append("\" clip-path=\"url(#").Append(ClipId).Append(")\"/>");
        }

        if (includeBorder)
        {
            builder.Append("<circle cx=\"50\" cy=\"50\" r=\"").Append(ToString(BorderCircleRadius))
                   .Append("\" fill=\"transparent\" stroke=\"#000\" stroke-width=\"5\"></circle>");
        }

        builder.Append("</g></svg>");
        return builder.ToString();
    }

    private static bool TryCreateDescriptor(object? data, out BadgeDescriptor descriptor)
    {
        descriptor = default;
        if (data is null)
            return false;

        JsonDocument? document = null;
        JsonElement root;

        try
        {
            if (data is JsonElement element)
                root = element;
            else
            {
                var json = JsonSerializer.Serialize(data);
                document = JsonDocument.Parse(json);
                root = document.RootElement;
            }

            if (!root.TryGetProperty("color1", out var color1Element) ||
                !root.TryGetProperty("color2", out var color2Element) ||
                !root.TryGetProperty("color3", out var color3Element))
                return false;

            var color1 = ResolveColorToken(color1Element);
            var color2 = ResolveColorToken(color2Element);
            var color3 = ResolveColorToken(color3Element);

            var param = root.TryGetProperty("param", out var paramElement) && paramElement.TryGetDouble(out var paramValue)
                ? paramValue
                : 0;
            var flip = root.TryGetProperty("flip", out var flipElement) && flipElement.ValueKind == JsonValueKind.True;

            if (!root.TryGetProperty("type", out var typeElement))
            {
                descriptor = new BadgeDescriptor(color1, color2, color3, param, null, null, null, flip);
                return true;
            }

            if (typeElement.ValueKind == JsonValueKind.Number && typeElement.TryGetInt32(out var numericType))
            {
                descriptor = new BadgeDescriptor(color1, color2, color3, param, numericType, null, null, flip);
                return true;
            }

            if (typeElement.ValueKind == JsonValueKind.Object &&
                typeElement.TryGetProperty("path1", out var path1Element) &&
                path1Element.ValueKind == JsonValueKind.String)
            {
                var path2 = typeElement.TryGetProperty("path2", out var path2Element) && path2Element.ValueKind == JsonValueKind.String
                    ? path2Element.GetString()
                    : null;

                descriptor = new BadgeDescriptor(color1, color2, color3, param, null, path1Element.GetString(), path2, flip);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
        finally
        {
            document?.Dispose();
        }
    }

    private static string ResolveColor(string? color)
        => string.IsNullOrWhiteSpace(color) ? DefaultColor : color;

    private static string ResolveColorToken(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
            return element.GetString() ?? DefaultColor;

        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var index) &&
            index >= 0 && index < Palette.Count)
            return Palette[index];

        return DefaultColor;
    }

    private static string HslToHex(double h, double s, double l)
    {
        var c = (1 - Math.Abs((2 * l) - 1)) * s;
        var hPrime = h / 60.0;
        var x = c * (1 - Math.Abs((hPrime % 2) - 1));

        double r1, g1, b1;
        if (double.IsNaN(h) || double.IsInfinity(h))
            r1 = g1 = b1 = 0;
        else if (hPrime >= 0 && hPrime < 1)
        {
            r1 = c; g1 = x; b1 = 0;
        }
        else if (hPrime >= 1 && hPrime < 2)
        {
            r1 = x; g1 = c; b1 = 0;
        }
        else if (hPrime >= 2 && hPrime < 3)
        {
            r1 = 0; g1 = c; b1 = x;
        }
        else if (hPrime >= 3 && hPrime < 4)
        {
            r1 = 0; g1 = x; b1 = c;
        }
        else if (hPrime >= 4 && hPrime < 5)
        {
            r1 = x; g1 = 0; b1 = c;
        }
        else
        {
            r1 = c; g1 = 0; b1 = x;
        }

        var m = l - (c / 2);
        var r = (int)Math.Round((r1 + m) * 255);
        var g = (int)Math.Round((g1 + m) * 255);
        var b = (int)Math.Round((b1 + m) * 255);

        return $"#{Clamp(r):X2}{Clamp(g):X2}{Clamp(b):X2}";

        static int Clamp(int value) => Math.Max(0, Math.Min(255, value));
    }

    private static string ToString(double value)
        => value.ToString("0.########", CultureInfo.InvariantCulture);

    private static IReadOnlyList<string> BuildPalette()
    {
        var colors = new List<string> { HslToHex(0, 0, 0.8) };

        for (var i = 0; i < 19; i++)
            colors.Add(HslToHex(i * 360.0 / 19.0, 0.6, 0.8));

        colors.Add(HslToHex(0, 0, 0.5));
        for (var i = 0; i < 19; i++)
            colors.Add(HslToHex(i * 360.0 / 19.0, 0.7, 0.5));

        colors.Add(HslToHex(0, 0, 0.3));
        for (var i = 0; i < 19; i++)
            colors.Add(HslToHex(i * 360.0 / 19.0, 0.4, 0.3));

        colors.Add(HslToHex(0, 0, 0.1));
        for (var i = 0; i < 19; i++)
            colors.Add(HslToHex(i * 360.0 / 19.0, 0.5, 0.1));

        return colors;
    }

    private static IReadOnlyDictionary<int, BadgePathDefinition> BuildPathDefinitions()
    {
        return new Dictionary<int, BadgePathDefinition>
        {
            [1] = new(CalcType1),
            [2] = new(CalcType2),
            [3] = new(CalcType3, 180),
            [4] = new(CalcType4, 90),
            [5] = new(CalcType5, 45),
            [6] = new(CalcType6, 90),
            [7] = new(CalcType7, 90),
            [8] = new(CalcType8, 90),
            [9] = new(CalcType9, 180),
            [10] = new(CalcType10, 90),
            [11] = new(CalcType11, 90),
            [12] = new(CalcType12, 180),
            [13] = new(CalcType13, 180),
            [14] = new(CalcType14, 180),
            [15] = new(CalcType15, 180),
            [16] = new(CalcType16),
            [17] = new(CalcType17),
            [18] = new(CalcType18, 180),
            [19] = new(CalcType19, 180),
            [20] = new(CalcType20, 90),
            [21] = new(CalcType21, 45),
            [22] = new(CalcType22, 45),
            [23] = new(CalcType23, 90),
            [24] = new(CalcType24, 180)
        };
    }

    private static BadgePathResult CalcType1(double param)
    {
        var vert = param > 0 ? param * 30 / 100 : 0;
        var hor = param < 0 ? -param * 30 / 100 : 0;

        var path1 = $"M 50 {ToString(100 - vert)} L {ToString(hor)} 50 H {ToString(100 - hor)} Z";
        var path2 = $"M {ToString(hor)} 50 H {ToString(100 - hor)} L 50 {ToString(vert)} Z";
        return new BadgePathResult(path1, path2);
    }

    private static BadgePathResult CalcType2(double param)
    {
        var x = param > 0 ? param * 30 / 100 : 0;
        var y = param < 0 ? -param * 30 / 100 : 0;

        var path1 = $"M {ToString(x)} {ToString(y)} L 50 50 L {ToString(100 - x)} {ToString(y)} V -1 H -1 Z";
        var path2 = $"M {ToString(x)} {ToString(100 - y)} L 50 50 L {ToString(100 - x)} {ToString(100 - y)} V 101 H -1 Z";
        return new BadgePathResult(path1, path2);
    }

    private static BadgePathResult CalcType3(double param)
    {
        var angle = (Math.PI / 4) + (Math.PI / 4 * (param + 100) / 200);
        var angle1 = -Math.PI / 2;
        var angle2 = (Math.PI / 2) + (Math.PI / 3);
        var angle3 = (Math.PI / 2) - (Math.PI / 3);

        string Triangle(double baseAngle)
        {
            var x1 = 50 + (100 * Math.Cos(baseAngle - (angle / 2)));
            var y1 = 50 + (100 * Math.Sin(baseAngle - (angle / 2)));
            var x2 = 50 + (100 * Math.Cos(baseAngle + (angle / 2)));
            var y2 = 50 + (100 * Math.Sin(baseAngle + (angle / 2)));
            return $"M 50 50 L {ToString(x1)} {ToString(y1)} L {ToString(x2)} {ToString(y2)} Z";
        }

        var path1 = Triangle(angle1);
        var path2 = Triangle(angle2) + "\n" + Triangle(angle3);
        return new BadgePathResult(path1, path2);
    }

    private static BadgePathResult CalcType4(double param)
    {
        param += 100;
        var y1 = 50 - (param * 30 / 200);
        var y2 = 50 + (param * 30 / 200);

        var path1 = $"M 0 {ToString(y2)} H 100 V 100 H 0 Z";
        var path2 = param > 0 ? $"M 0 {ToString(y1)} H 100 V {ToString(y2)} H 0 Z" : string.Empty;
        return new BadgePathResult(path1, string.IsNullOrEmpty(path2) ? null : path2);
    }

    private static BadgePathResult CalcType5(double param)
    {
        param += 100;
        var x1 = 50 - (param * 10 / 200) - 10;
        var x2 = 50 + (param * 10 / 200) + 10;

        var path1 = $"M {ToString(x1)} 0 H {ToString(x2)} V 100 H {ToString(x1)} Z";
        var path2 = $"M 0 {ToString(x1)} H 100 V {ToString(x2)} H 0 Z";
        return new BadgePathResult(path1, path2);
    }

    private static BadgePathResult CalcType6(double param)
    {
        var width = 5 + ((param + 100) * 8 / 200);
        const double x1 = 50;
        const double x2 = 20;
        const double x3 = 80;

        var path1 = $"M {ToString(x1 - width)} 0 H {ToString(x1 + width)} V 100 H {ToString(x1 - width)}";
        var path2 = $"M {ToString(x2 - width)} 0 H {ToString(x2 + width)} V 100 H {ToString(x2 - width)} Z\n" +
                    $"M {ToString(x3 - width)} 0 H {ToString(x3 + width)} V 100 H {ToString(x3 - width)} Z";
        return new BadgePathResult(path1, path2);
    }

    private static BadgePathResult CalcType7(double param)
    {
        var w = 20 + (param * 10 / 100);
        var path1 = "M 0 50 Q 25 30 50 50 T 100 50 V 100 H 0 Z";
        var path2 = $"M 0 {ToString(50 - w)} Q 25 {ToString(30 - w)} 50 {ToString(50 - w)} T 100 {ToString(50 - w)}\n" +
                    $"                            V {ToString(50 + w)} Q 75 {ToString(70 + w)} 50 {ToString(50 + w)} T 0 {ToString(50 + w)} Z";
        return new BadgePathResult(path1, path2);
    }

    private static BadgePathResult CalcType8(double param)
    {
        var y = param * 20 / 100;
        var path1 = "M 0 50 H 100 V 100 H 0 Z";
        var path2 = $"M 0 50 Q 50 {ToString(y)} 100 50 Q 50 {ToString(100 - y)} 0 50 Z";
        return new BadgePathResult(path1, path2);
    }

    private static BadgePathResult CalcType9(double param)
    {
        var y1 = param > 0 ? param / 100 * 20 : 0;
        var y2 = param < 0 ? 50 + (param / 100 * 30) : 50;
        const double h = 70;

        var path1 = $"M 50 {ToString(y1)} L 100 {ToString(y1 + h)} V 101 H 0 V {ToString(y1 + h)} Z";
        var path2 = $"M 50 {ToString(y1 + y2)} L 100 {ToString(y1 + y2 + h)} V 101 H 0 V {ToString(y1 + y2 + h)} Z";
        return new BadgePathResult(path1, path2);
    }

    private static BadgePathResult CalcType10(double param)
    {
        var r = 30.0;
        var d = 7.0;

        if (param > 0) r += param * 50 / 100;
        if (param < 0) d -= param * 20 / 100;

        var path1 = $"M {ToString(50 + d + r)} {ToString(50 - r)} A {ToString(r)} {ToString(r)} 0 0 0 {ToString(50 + d + r)} {ToString(50 + r)} H 101 V {ToString(50 - r)} Z";
        var path2 = $"M {ToString(50 - d - r)} {ToString(50 - r)} A {ToString(r)} {ToString(r)} 0 0 1 {ToString(50 - d - r)} {ToString(50 + r)} H -1 V {ToString(50 - r)} Z";
        return new BadgePathResult(path1, path2);
    }

    private static BadgePathResult CalcType11(double param)
    {
        var a1 = 30.0;
        var a2 = 30.0;
        var x = 50 - (50 * Math.Cos(Math.PI / 4));
        var y = 50 - (50 * Math.Sin(Math.PI / 4));

        if (param > 0)
        {
            a1 += param * 25 / 100;
            a2 += param * 25 / 100;
        }
        if (param < 0)
            a2 -= param * 50 / 100;

        var path1 = $"M {ToString(x)} {ToString(y)} Q {ToString(a1)} 50 {ToString(x)} {ToString(100 - y)} H 0 V {ToString(y)} Z\n" +
                    $"                          M {ToString(100 - x)} {ToString(y)} Q {ToString(100 - a1)} 50 {ToString(100 - x)} {ToString(100 - y)} H 100 V {ToString(y)} Z";
        var path2 = $"M {ToString(x)} {ToString(y)} Q 50 {ToString(a2)} {ToString(100 - x)} {ToString(y)} V 0 H {ToString(x)} Z\n" +
                    $"                          M {ToString(x)} {ToString(100 - y)} Q 50 {ToString(100 - a2)} {ToString(100 - x)} {ToString(100 - y)} V 100 H {ToString(x)} Z";
        return new BadgePathResult(path1, path2);
    }

    private static BadgePathResult CalcType12(double param)
    {
        var a1 = 30.0;
        var a2 = 35.0;
        if (param > 0) a1 += param * 30 / 100;
        if (param < 0) a2 += param * 15 / 100;

        var path1 = $"M 0 {ToString(a1)} H 100 V 100 H 0 Z";
        var path2 = $"M 0 {ToString(a1)} H {ToString(a2)} V 100 H 0 Z\n                          M 100 {ToString(a1)} H {ToString(100 - a2)} V 100 H 100 Z";
        return new BadgePathResult(path1, path2);
    }

    private static BadgePathResult CalcType13(double param)
    {
        var r = 30.0;
        var d = 0.0;
        if (param > 0) r += param * 50 / 100;
        if (param < 0) d -= param * 20 / 100;

        var path1 = "M 0 0 H 50 V 100 H 0 Z";
        var path2 = $"M {ToString(50 - r)} {ToString(50 - d - r)} A {ToString(r)} {ToString(r)} 0 0 0 {ToString(50 + r)} {ToString(50 - r - d)} V 0 H {ToString(50 - r)} Z";
        return new BadgePathResult(path1, path2);
    }

    private static BadgePathResult CalcType14(double param)
    {
        var a = (Math.PI / 4) + (param * Math.PI / 4 / 100);
        var d = 0.0;

        var path1 = $"M 50 0 Q 50 {ToString(50 + d)} {ToString(50 + (50 * Math.Cos(a)))} {ToString(50 + (50 * Math.Sin(a)))} H 100 V 0 H 50 Z";
        var path2 = $"M 50 0 Q 50 {ToString(50 + d)} {ToString(50 - (50 * Math.Cos(a)))} {ToString(50 + (50 * Math.Sin(a)))} H 0 V 0 H 50 Z";
        return new BadgePathResult(path1, path2);
    }

    private static BadgePathResult CalcType15(double param)
    {
        var w = 13 + (param * 6 / 100);
        const double r1 = 80;
        const double r2 = 45;
        const double d = 10;

        var path1 = $"M {ToString(50 - r1 - w)} {ToString(100 + d)} A {ToString(r1 + w)} {ToString(r1 + w)} 0 0 1 {ToString(50 + r1 + w)} {ToString(100 + d)}\n" +
                    $"                                   H {ToString(50 + r1 - w)} A {ToString(r1 - w)} {ToString(r1 - w)} 0 1 0 {ToString(50 - r1 + w)} {ToString(100 + d)}";
        var path2 = $"M {ToString(50 - r2 - w)} {ToString(100 + d)} A {ToString(r2 + w)} {ToString(r2 + w)} 0 0 1 {ToString(50 + r2 + w)} {ToString(100 + d)}\n" +
                    $"                                   H {ToString(50 + r2 - w)} A {ToString(r2 - w)} {ToString(r2 - w)} 0 1 0 {ToString(50 - r2 + w)} {ToString(100 + d)}";
        return new BadgePathResult(path1, path2);
    }

    private static BadgePathResult CalcType16(double param)
    {
        var a = 30 * Math.PI / 180;
        var d = 25.0;

        if (param > 0) a += 30 * Math.PI / 180 * param / 100;
        if (param < 0) d += param * 25 / 100;

        var builder1 = new StringBuilder();
        var builder2 = new StringBuilder();

        for (var i = 0; i < 3; i++)
        {
            var angle1 = (i * Math.PI * 2 / 3) + (a / 2) - (Math.PI / 2);
            var angle2 = (i * Math.PI * 2 / 3) - (a / 2) - (Math.PI / 2);

            builder1.Append($"M {ToString(50 + (100 * Math.Cos(angle1)))} {ToString(50 + (100 * Math.Sin(angle1)))}\n");
            builder1.Append($"                               L {ToString(50 + (100 * Math.Cos(angle2)))} {ToString(50 + (100 * Math.Sin(angle2)))}\n");
            builder1.Append($"                               L {ToString(50 + (d * Math.Cos(angle2)))} {ToString(50 + (d * Math.Sin(angle2)))}\n");
            builder1.Append($"                               A {ToString(d)} {ToString(d)} 0 0 1 {ToString(50 + (d * Math.Cos(angle1)))} {ToString(50 + (d * Math.Sin(angle1)))} Z");
        }

        for (var i = 0; i < 3; i++)
        {
            var angle1 = (i * Math.PI * 2 / 3) + (a / 2) + (Math.PI / 2);
            var angle2 = (i * Math.PI * 2 / 3) - (a / 2) + (Math.PI / 2);

            builder2.Append($"M {ToString(50 + (100 * Math.Cos(angle1)))} {ToString(50 + (100 * Math.Sin(angle1)))}\n");
            builder2.Append($"                               L {ToString(50 + (100 * Math.Cos(angle2)))} {ToString(50 + (100 * Math.Sin(angle2)))}\n");
            builder2.Append($"                               L {ToString(50 + (d * Math.Cos(angle2)))} {ToString(50 + (d * Math.Sin(angle2)))}\n");
            builder2.Append($"                               A {ToString(d)} {ToString(d)} 0 0 1 {ToString(50 + (d * Math.Cos(angle1)))} {ToString(50 + (d * Math.Sin(angle1)))} Z");
        }

        return new BadgePathResult(builder1.ToString(), builder2.ToString());
    }

    private static BadgePathResult CalcType17(double param)
    {
        var w = 35.0;
        var h = 45.0;

        if (param > 0) w += param * 20 / 100;
        if (param < 0) h -= param * 30 / 100;

        var path1 = $"M 50 45 L {ToString(50 - w)} {ToString(h + 45)} H {ToString(50 + w)} Z";
        var path2 = $"M 50 0 L {ToString(50 - w)} {ToString(h)} H {ToString(50 + w)} Z";
        return new BadgePathResult(path1, path2);
    }

    private static BadgePathResult CalcType18(double param)
    {
        var a = 90 * Math.PI / 180;
        var d = 10.0;

        if (param > 0) a -= 60 / 180.0 * Math.PI * param / 100;
        if (param < 0) d -= param * 15 / 100;

        var builder1 = new StringBuilder();
        var builder2 = new StringBuilder();

        for (var i = 0; i < 3; i++)
        {
            var angle1 = (Math.PI * 2 / 3 * i) + (a / 2) - (Math.PI / 2);
            var angle2 = (Math.PI * 2 / 3 * i) - (a / 2) - (Math.PI / 2);
            var mid = (angle1 + angle2) / 2;
            var path = $"M {ToString(50 + (100 * Math.Cos(angle1)))} {ToString(50 + (100 * Math.Sin(angle1)))}\n" +
                       $"                            L {ToString(50 + (100 * Math.Cos(angle2)))} {ToString(50 + (100 * Math.Sin(angle2)))}\n" +
                       $"                            L {ToString(50 + (d * Math.Cos(mid)))} {ToString(50 + (d * Math.Sin(mid)))} Z";

            if (i == 0)
                builder1.Append(path);
            else
                builder2.Append(path);
        }

        return new BadgePathResult(builder1.ToString(), builder2.ToString());
    }

    private static BadgePathResult CalcType19(double param)
    {
        var w1 = 60 + (param * 20 / 100);
        var w2 = 20 + (param * 20 / 100);

        var path1 = $"M 50 -10 L {ToString(50 - w1)} 100 H {ToString(50 + w1)} Z";
        var path2 = w2 > 0
            ? $"M 50 0 L {ToString(50 - w2)} 100 H {ToString(50 + w2)} Z"
            : null;
        return new BadgePathResult(path1, path2);
    }

    private static BadgePathResult CalcType20(double param)
    {
        var w = 10 + (param > 0 ? param * 20 / 100 : 0);
        var h = 20 + (param < 0 ? param * 40 / 100 : 0);

        var path1 = $"M 0 {ToString(50 - h)} H {ToString(50 - w)} V 100 H 0 Z";
        var path2 = $"M {ToString(50 + w)} 0 V {ToString(50 + h)} H 100 V 0 Z";
        return new BadgePathResult(path1, path2);
    }

    private static BadgePathResult CalcType21(double param)
    {
        var w = 40 - (param > 0 ? param * 20 / 100 : 0);
        var h = 50 + (param < 0 ? param * 20 / 100 : 0);

        var path1 = $"M 50 {ToString(h)} Q {ToString(50 + w)} 0 50 0 T 50 {ToString(h)} Z\n" +
                    $"                          M 50 {ToString(100 - h)} Q {ToString(50 + w)} 100 50 100 T 50 {ToString(100 - h)} Z";
        var path2 = $"M {ToString(h)} 50 Q 0 {ToString(50 + w)} 0 50 T {ToString(h)} 50 Z\n" +
                    $"                          M {ToString(100 - h)} 50 Q 100 {ToString(50 + w)} 100 50 T {ToString(100 - h)} 50 Z";
        return new BadgePathResult(path1, path2);
    }

    private static BadgePathResult CalcType22(double param)
    {
        var w = 20 + (param * 10 / 100);

        var path1 = $"M {ToString(50 - w)} {ToString(50 - w)} H {ToString(50 + w)} V {ToString(50 + w)} H {ToString(50 - w)} Z";
        var builder = new StringBuilder();
        for (var i = -4; i < 4; i++)
        {
            for (var j = -4; j < 4; j++)
            {
                var a = (i + j) % 2;
                builder.Append($"M {ToString(50 - w - (w * 2 * i))} {ToString(50 - w - (w * 2 * (j + a)))} h {ToString(-w * 2)} v {ToString(w * 2)} h {ToString(w * 2)} Z");
            }
        }
        return new BadgePathResult(path1, builder.ToString());
    }

    private static BadgePathResult CalcType23(double param)
    {
        var w = 17 + (param > 0 ? param * 35 / 100 : 0);
        var h = 25 - (param < 0 ? param * 23 / 100 : 0);

        var builder1 = new StringBuilder();
        for (var i = -4; i <= 4; i++) builder1.Append($"M {ToString(50 - (w * i * 2))} {ToString(50 - h)} l {ToString(-w)} {ToString(-h)} l {ToString(-w)} {ToString(h)} l {ToString(w)} {ToString(h)} Z");

        var builder2 = new StringBuilder();
        for (var i = -4; i <= 4; i++) builder2.Append($"M {ToString(50 - (w * i * 2))} {ToString(50 + h)} l {ToString(-w)} {ToString(-h)} l {ToString(-w)} {ToString(h)} l {ToString(w)} {ToString(h)} Z");

        return new BadgePathResult(builder1.ToString(), builder2.ToString());
    }

    private static BadgePathResult CalcType24(double param)
    {
        var w = 50 + (param > 0 ? param * 60 / 100 : 0);
        var h = 45 + (param < 0 ? param * 30 / 100 : 0);

        var path1 = $"M 0 {ToString(h)} L 50 70 L 100 {ToString(h)} V 100 H 0 Z";
        var path2 = $"M 50 0 L {ToString(50 + w)} 100 H 100 V {ToString(h)} L 50 70 L 0 {ToString(h)} V 100 H {ToString(50 - w)} Z";
        return new BadgePathResult(path1, path2);
    }

    private readonly record struct BadgeDescriptor(string Color1, string Color2, string Color3, double Param,
                                                   int? NumericType, string? CustomPath1, string? CustomPath2, bool Flip);

    private readonly record struct BadgePathDefinition(Func<double, BadgePathResult> Calculator, double? FlipRotationDegrees = null);

    private readonly record struct BadgePathResult(string Path1, string? Path2);
}
