using System.Reflection;
using ScreepsDotNet.Common.Constants;

namespace ScreepsDotNet.Driver.Tests.Constants;

public sealed class StringLiteralGuardTests
{
    [Fact]
    public void RoomObjectTypes_AreReferencedViaConstants()
        => AssertNoLiteralUsage(RoomObjectTypesFileName, GetRoomObjectTypeLiterals());

    [Fact]
    public void IntentKeys_AreReferencedViaConstants()
        => AssertNoLiteralUsage(IntentKeysFileName, GetIntentKeyLiterals());

    private static void AssertNoLiteralUsage(string excludedFileName, IReadOnlyCollection<string> literals)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../ScreepsDotNet.Driver"));
        var files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                             .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                                         && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                                         && !file.EndsWith(excludedFileName, StringComparison.OrdinalIgnoreCase))
                             .ToArray();

        foreach (var literal in literals)
        {
            var needle = $"\"{literal}\"";
            Assert.DoesNotContain(files, file => File.ReadAllText(file).Contains(needle, StringComparison.Ordinal));
        }
    }

    private static IReadOnlyCollection<string> GetRoomObjectTypeLiterals()
        => typeof(RoomObjectTypes).GetFields(BindingFlags.Public | BindingFlags.Static)
                                  .Select(field => (string)field.GetValue(null)!)
                                  .ToArray();

    private static IReadOnlyCollection<string> GetIntentKeyLiterals()
        => typeof(IntentKeys).GetFields(BindingFlags.Public | BindingFlags.Static)
                             .Select(field => (string)field.GetValue(null)!)
                             .ToArray();

    private static string RoomObjectTypesFileName => $"{Path.DirectorySeparatorChar}RoomObjectTypes.cs";
    private static string IntentKeysFileName => $"{Path.DirectorySeparatorChar}IntentKeys.cs";
}
