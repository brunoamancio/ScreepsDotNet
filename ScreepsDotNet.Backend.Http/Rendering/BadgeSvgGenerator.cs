using System.Reflection;
using System.Text.Json;
using Jint;
using ScreepsDotNet.Backend.Core.Services;

namespace ScreepsDotNet.Backend.Http.Rendering;

internal sealed class BadgeSvgGenerator : IBadgeSvgGenerator
{
    private const string ResourceName = "ScreepsDotNet.Backend.Http.Rendering.Scripts.badge-generator.js";
    private const string EmptySvg = "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>";

    private static readonly Lock EngineLock = new();

    private readonly Engine _engine;

    public BadgeSvgGenerator()
    {
        _engine = new Engine(options => options.LimitRecursion(512));
        var script = LoadScript();
        _engine.Execute(script);
    }

    public string GenerateSvg(object? badgeData, bool includeBorder)
    {
        if (badgeData is null)
            return EmptySvg;

        string json;
        try
        {
            json = JsonSerializer.Serialize(badgeData);
        }
        catch
        {
            return EmptySvg;
        }

        lock (EngineLock)
        {
            try
            {
                var parsed = _engine.Invoke("JSON.parse", json);
                var result = _engine.Invoke("__badge_getSvg", parsed, includeBorder);
                return result.IsString() ? result.AsString() : EmptySvg;
            }
            catch
            {
                return EmptySvg;
            }
        }
    }

    private static string LoadScript()
    {
        var assembly = typeof(BadgeSvgGenerator).GetTypeInfo().Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
            throw new InvalidOperationException($"Embedded resource '{ResourceName}' was not found.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
