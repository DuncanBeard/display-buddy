namespace TaskBard.Tests;

/// <summary>Locates the repository root (the folder holding TaskBard.slnx) from the test output folder.</summary>
internal static class RepoRoot
{
    public static string Path { get; } = Find();

    public static string File(string relativePath) =>
        System.IO.Path.Combine(Path, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));

    private static string Find()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "TaskBard.slnx")))
                return dir.FullName;
        }
        throw new InvalidOperationException($"TaskBard.slnx not found above {AppContext.BaseDirectory}");
    }
}
