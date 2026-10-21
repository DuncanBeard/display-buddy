using System.Xml.Linq;

namespace TaskBard.Tests;

/// <summary>
/// Reads the solution, project files and MSIX manifest as XML and checks
/// that they agree on TaskBard's identity.
/// </summary>
[TestClass]
public sealed class ReleaseMetadataTests
{
    private const string AppProject = @"..\TaskBard\TaskBard.csproj";
    private const string PackagedExecutable = @"TaskBard\TaskBard.exe";
    private const string Description = "Switches Windows 11 taskbar profiles based on your primary display.";

    private static readonly XNamespace Foundation = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
    private static readonly XNamespace Uap = "http://schemas.microsoft.com/appx/manifest/uap/windows10";
    private static readonly XNamespace Desktop = "http://schemas.microsoft.com/appx/manifest/desktop/windows10";

    [TestMethod]
    public void Solution_contains_the_app_and_test_projects()
    {
        var paths = Load("TaskBard.slnx").Descendants("Project")
            .Select(p => (string?)p.Attribute("Path"))
            .ToList();

        CollectionAssert.Contains(paths, "TaskBard/TaskBard.csproj");
        CollectionAssert.Contains(paths, "TaskBard.Tests/TaskBard.Tests.csproj");
    }

    [TestMethod]
    public void App_project_builds_TaskBard_exe()
    {
        var project = Load("TaskBard/TaskBard.csproj");

        Assert.AreEqual("WinExe", Property(project, "OutputType"));
        // With no AssemblyName override the assembly is named after TaskBard.csproj.
        Assert.AreEqual("TaskBard", Property(project, "AssemblyName") ?? "TaskBard");
    }

    [TestMethod]
    public void App_project_has_no_package_or_project_references()
    {
        var project = Load("TaskBard/TaskBard.csproj");

        Assert.IsEmpty(Elements(project, "PackageReference").ToList(), "The app must stay dependency-free.");
        Assert.IsEmpty(Elements(project, "ProjectReference").ToList(), "The app must not reference other projects (including tests).");
    }

    [TestMethod]
    public void Test_project_references_the_app_project()
    {
        var references = Includes(Load("TaskBard.Tests/TaskBard.Tests.csproj"), "ProjectReference");

        CollectionAssert.AreEqual(new[] { AppProject }, references);
    }

    [TestMethod]
    public void Package_project_packages_the_app_project()
    {
        var wapproj = Load("TaskBard.Package/TaskBard.Package.wapproj");

        CollectionAssert.AreEqual(new[] { AppProject }, Includes(wapproj, "ProjectReference"));
        Assert.AreEqual(AppProject, Property(wapproj, "EntryPointProjectUniqueName"));
    }

    [TestMethod]
    public void Manifest_display_names_are_TaskBard()
    {
        var manifest = Load("TaskBard.Package/Package.appxmanifest");

        Assert.AreEqual("TaskBard", manifest.Descendants(Foundation + "Properties").Single()
            .Element(Foundation + "DisplayName")?.Value);
        Assert.AreEqual("TaskBard", (string?)VisualElements(manifest).Attribute("DisplayName"));
    }

    [TestMethod]
    public void Manifest_description_states_what_TaskBard_does()
    {
        var manifest = Load("TaskBard.Package/Package.appxmanifest");

        Assert.AreEqual(Description, (string?)VisualElements(manifest).Attribute("Description"));
    }

    [TestMethod]
    public void Manifest_application_runs_TaskBard_exe()
    {
        var application = Load("TaskBard.Package/Package.appxmanifest")
            .Descendants(Foundation + "Application").Single();

        Assert.AreEqual(PackagedExecutable, (string?)application.Attribute("Executable"));
    }

    [TestMethod]
    public void Manifest_startup_task_has_the_TaskBard_identity()
    {
        var manifest = Load("TaskBard.Package/Package.appxmanifest");
        var extension = manifest.Descendants(Desktop + "Extension")
            .Single(e => (string?)e.Attribute("Category") == "windows.startupTask");
        var startupTask = extension.Element(Desktop + "StartupTask");

        Assert.IsNotNull(startupTask);
        Assert.AreEqual(PackagedExecutable, (string?)extension.Attribute("Executable"));
        Assert.AreEqual("TaskBardStartup", (string?)startupTask.Attribute("TaskId"));
        Assert.AreEqual("TaskBard", (string?)startupTask.Attribute("DisplayName"));
    }

    private static XDocument Load(string relativePath) => XDocument.Load(RepoRoot.File(relativePath));

    private static XElement VisualElements(XDocument manifest) =>
        manifest.Descendants(Uap + "VisualElements").Single();

    // MSBuild elements are matched by local name: SDK-style projects have no
    // XML namespace, the wapproj uses the legacy MSBuild 2003 namespace.
    private static IEnumerable<XElement> Elements(XDocument project, string name) =>
        project.Descendants().Where(e => e.Name.LocalName == name);

    private static string? Property(XDocument project, string name) =>
        Elements(project, name).SingleOrDefault(e => e.Parent?.Name.LocalName == "PropertyGroup")?.Value;

    private static string[] Includes(XDocument project, string itemName) =>
        Elements(project, itemName).Select(e => (string?)e.Attribute("Include") ?? "").ToArray();
}
