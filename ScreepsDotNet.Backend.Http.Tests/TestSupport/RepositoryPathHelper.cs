namespace ScreepsDotNet.Backend.Http.Tests.TestSupport;

internal static class RepositoryPathHelper
{
    private const string SolutionFileName = "ScreepsDotNet.slnx";

    public static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, SolutionFileName)))
            current = current.Parent;

        return current is null ? throw new DirectoryNotFoundException($"Could not locate repository root (expected {SolutionFileName}).") : current.FullName;
    }
}
