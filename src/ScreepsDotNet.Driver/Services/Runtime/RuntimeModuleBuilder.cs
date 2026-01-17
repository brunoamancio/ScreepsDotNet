using System.Security.Cryptography;
using System.Text;

namespace ScreepsDotNet.Driver.Services.Runtime;

internal static class RuntimeModuleBuilder
{
    public static IReadOnlyDictionary<string, string> NormalizeModules(IReadOnlyDictionary<string, string> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        var normalized = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, code) in modules) {
            if (string.IsNullOrWhiteSpace(name))
                continue;
            normalized[name.Trim()] = code;
        }

        return normalized;
    }

    public static string ComputeCodeHash(IReadOnlyDictionary<string, string> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        if (modules.Count == 0)
            return string.Empty;

        var builder = new StringBuilder();
        foreach (var (name, code) in modules.OrderBy(pair => pair.Key, StringComparer.Ordinal)) {
            builder.Append(name);
            builder.Append('\n');
            builder.Append(code);
            builder.Append('\n');
        }

        var buffer = Encoding.UTF8.GetBytes(builder.ToString());
        return Convert.ToHexString(SHA256.HashData(buffer));
    }

    public static string BuildBundle(IReadOnlyDictionary<string, string> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        if (modules.Count == 0)
            return string.Empty;

        var ordered = modules.Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
                             .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                             .ToArray();
        if (ordered.Length == 0)
            return string.Empty;

        var builder = new StringBuilder();
        builder.AppendLine("(function(){");
        builder.AppendLine("const modules = {");
        for (var i = 0; i < ordered.Length; i++) {
            var (name, code) = ordered[i];
            builder.Append('"');
            builder.Append(EscapeModuleName(name));
            builder.Append("\":function(module, exports, require){\n");
            builder.AppendLine(code);
            builder.Append('}');
            if (i < ordered.Length - 1)
                builder.Append(',');
            builder.AppendLine();
        }
        builder.AppendLine("};");
        builder.AppendLine("const cache = {};");
        builder.AppendLine("const requireModule = name => {");
        builder.AppendLine("  if(!modules[name]) throw new Error(`Module '${name}' not found.`);");
        builder.AppendLine("  if(!cache[name]) {");
        builder.AppendLine("    const module = { exports: {} };");
        builder.AppendLine("    cache[name] = module;");
        builder.AppendLine("    modules[name](module, module.exports, requireModule);");
        builder.AppendLine("  }");
        builder.AppendLine("  return cache[name].exports;");
        builder.AppendLine("};");
        builder.AppendLine("const mainModule = requireModule('main');");
        builder.AppendLine("if(mainModule && typeof mainModule.loop === 'function')");
        builder.AppendLine("  mainModule.loop();");
        builder.AppendLine("})();");
        return builder.ToString();
    }

    private static string EscapeModuleName(string name) =>
        name.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
