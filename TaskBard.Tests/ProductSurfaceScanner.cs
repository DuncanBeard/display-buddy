using System.Text.RegularExpressions;

namespace TaskBard.Tests;

internal sealed record ScanFinding(string RelativePath, int Line, string Match, string Rule)
{
    public override string ToString() => $"{RelativePath}:{Line}: {Rule}: '{Match}'";
}

/// <summary>
/// Finds abandoned product names and stale repository URLs in the repo's
/// active product surfaces. Every text file is scanned unless it is on the
/// explicit allow-list of historical records, so new files are covered
/// without anyone having to remember to register them.
/// </summary>
internal static class ProductSurfaceScanner
{
    public const string CurrentRepo = "taskbard";

    private static readonly Regex AbandonedName = new(
        @"Taskbar[\s_-]*Alignment[\s_-]*Tool|Display[\s_-]*Buddy",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex OwnedRepoUrl = new(
        @"(?:github\.com|githubusercontent\.com)/DuncanBeard/(?<repo>[A-Za-z0-9_.-]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // Historical records keep the names that were accurate when they were written.
    private static readonly string[] AllowedPathPrefixes =
    [
        "docs/adr/",    // architecture decision records
        "docs/issues/", // dated issue records
        "specs/",       // completed specs
    ];

    private static readonly Regex AllowedRootFile = new(
        @"^copilot-session-[^/]+\.md$", // captured session transcripts
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly string[] AllowedFiles =
    [
        "TaskBard.Tests/AbandonedNameScannerTests.cs", // spells out the names to test the scanner
    ];

    // The glossary's "_Avoid_:" lines exist to name retired terms.
    private static readonly (string Path, string LinePrefix)[] AllowedLines =
    [
        ("CONTEXT.md", "_Avoid_:"),
    ];

    private static readonly HashSet<string> SkippedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", ".worktrees", "bin", "obj", "publish", "TestResults", "node_modules",
    };

    public static bool IsAllowListed(string relativePath)
    {
        var path = Normalize(relativePath);
        return AllowedPathPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase))
            || AllowedRootFile.IsMatch(path)
            || AllowedFiles.Contains(path, StringComparer.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<ScanFinding> ScanText(string relativePath, string content)
    {
        var path = Normalize(relativePath);
        var allowedPrefixes = AllowedLines
            .Where(a => a.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
            .Select(a => a.LinePrefix)
            .ToArray();

        var findings = new List<ScanFinding>();
        var lines = content.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (allowedPrefixes.Any(p => line.TrimStart().StartsWith(p, StringComparison.Ordinal)))
                continue;

            foreach (Match m in AbandonedName.Matches(line))
                findings.Add(new ScanFinding(path, i + 1, m.Value, "abandoned product name"));

            foreach (Match m in OwnedRepoUrl.Matches(line))
            {
                var repo = m.Groups["repo"].Value;
                if (repo.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                    repo = repo[..^4];
                if (!repo.Equals(CurrentRepo, StringComparison.OrdinalIgnoreCase))
                    findings.Add(new ScanFinding(path, i + 1, m.Value, "stale repository URL"));
            }
        }
        return findings;
    }

    public static IReadOnlyList<ScanFinding> ScanRepository(string repoRoot)
    {
        var findings = new List<ScanFinding>();
        foreach (var file in EnumerateFiles(repoRoot))
        {
            var relative = Normalize(Path.GetRelativePath(repoRoot, file));
            if (IsAllowListed(relative))
                continue;

            var bytes = File.ReadAllBytes(file);
            if (LooksBinary(bytes))
                continue;

            findings.AddRange(ScanText(relative, System.Text.Encoding.UTF8.GetString(bytes)));
        }
        return findings;
    }

    private static IEnumerable<string> EnumerateFiles(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory))
            yield return file;

        foreach (var sub in Directory.EnumerateDirectories(directory))
        {
            if (SkippedDirectories.Contains(Path.GetFileName(sub)))
                continue;
            foreach (var file in EnumerateFiles(sub))
                yield return file;
        }
    }

    private static bool LooksBinary(byte[] bytes) =>
        Array.IndexOf(bytes, (byte)0, 0, Math.Min(bytes.Length, 8000)) >= 0;

    private static string Normalize(string path) => path.Replace('\\', '/');
}
