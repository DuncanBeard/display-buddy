namespace TaskBard.Tests;

// This file is on the scanner's allow-list: it has to spell out the abandoned
// names to prove the scanner catches them.
[TestClass]
public sealed class AbandonedNameScannerTests
{
    [TestMethod]
    [DataRow("Taskbar Alignment Tool")]
    [DataRow("TaskbarAlignmentTool")]
    [DataRow("Display Buddy")]
    [DataRow("display-buddy")]
    [DataRow("DISPLAYBUDDY")]
    public void Flags_abandoned_product_names(string name)
    {
        var findings = ProductSurfaceScanner.ScanText("README.md", $"line one\nWelcome to {name}!\n");

        Assert.HasCount(1, findings);
        Assert.AreEqual(2, findings[0].Line);
        Assert.AreEqual(name, findings[0].Match);
    }

    [TestMethod]
    public void Accepts_the_current_product_name()
    {
        var findings = ProductSurfaceScanner.ScanText("TaskBard/Program.cs", "namespace TaskBard; // TaskBard.exe");

        Assert.IsEmpty(findings);
    }

    [TestMethod]
    [DataRow("https://github.com/DuncanBeard/some-old-repo")]
    [DataRow("https://raw.githubusercontent.com/DuncanBeard/old/main/schema/config.schema.json")]
    public void Flags_repo_urls_that_do_not_point_at_taskbard(string url)
    {
        var findings = ProductSurfaceScanner.ScanText("README.md", $"See {url} for details.");

        Assert.HasCount(1, findings);
    }

    [TestMethod]
    [DataRow("https://github.com/DuncanBeard/taskbard")]
    [DataRow("https://github.com/DuncanBeard/taskbard.git")]
    [DataRow("https://raw.githubusercontent.com/DuncanBeard/taskbard/main/schema/config.schema.json")]
    [DataRow("https://github.com/dotnet/vscode-csharp/blob/main/README.md")]
    public void Accepts_current_repo_urls_and_unrelated_github_urls(string url)
    {
        var findings = ProductSurfaceScanner.ScanText("README.md", $"See {url} for details.");

        Assert.IsEmpty(findings);
    }

    [TestMethod]
    [DataRow("docs/adr/0002-runatstartup-interface-and-adapters.md")]
    [DataRow("docs/issues/2026-05-27-foreground-activation-poison.md")]
    [DataRow("specs/001-hover-effective-resolution/plan.md")]
    [DataRow("copilot-session-a06673c4-2976-4828-a0c2-f9567d42fa68.md")]
    public void Historical_records_are_allow_listed(string relativePath)
    {
        Assert.IsTrue(ProductSurfaceScanner.IsAllowListed(relativePath));
    }

    [TestMethod]
    [DataRow("README.md")]
    [DataRow("AGENTS.md")]
    [DataRow("CONTEXT.md")]
    [DataRow("make.ps1")]
    [DataRow("TaskBard/TrayApplicationContext.cs")]
    [DataRow("TaskBard.Package/Package.appxmanifest")]
    [DataRow("schema/config.schema.json")]
    [DataRow("sandbox/setup-self-contained.ps1")]
    [DataRow("docs/agents/domain.md")]
    [DataRow("docs/copilot-session-notes.md")]
    public void Active_product_surfaces_are_not_allow_listed(string relativePath)
    {
        Assert.IsFalse(ProductSurfaceScanner.IsAllowListed(relativePath));
    }

    [TestMethod]
    public void Glossary_avoid_lines_may_name_retired_terms_but_other_glossary_lines_may_not()
    {
        const string context = "_Avoid_: Taskbar Alignment Tool, Display Buddy.\nThe app was called Display Buddy.\n";

        var findings = ProductSurfaceScanner.ScanText("CONTEXT.md", context);

        Assert.HasCount(1, findings);
        Assert.AreEqual(2, findings[0].Line);
    }

    [TestMethod]
    public void Avoid_line_exemption_only_applies_to_the_glossary()
    {
        var findings = ProductSurfaceScanner.ScanText("README.md", "_Avoid_: Display Buddy.");

        Assert.HasCount(1, findings);
    }
}
