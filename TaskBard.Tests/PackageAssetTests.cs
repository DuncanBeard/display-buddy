using System.Xml.Linq;

namespace TaskBard.Tests;

/// <summary>
/// Checks the committed icon and package-image files against what the MSIX
/// manifest and the app project expect: every reference resolves, every file
/// has its exact pixel size, and there is only one package-image tree.
/// </summary>
[TestClass]
public sealed class PackageAssetTests
{
    private const string Manifest = "TaskBard.Package/Package.appxmanifest";
    private const string PackageDirectory = "TaskBard.Package";
    private const string PackageImages = "TaskBard.Package/Images";

    // The sizes below restate the asset contract on purpose instead of reading
    // assets/generate.ps1, so a generator mistake can't also change the expectation.

    // Unscaled (scale-100) size of each image the manifest can reference.
    private static readonly Dictionary<string, (int Width, int Height)> BaseSizes = new()
    {
        ["Logo"] = (50, 50),
        ["Square44x44Logo"] = (44, 44),
        ["Square71x71Logo"] = (71, 71),
        ["Square150x150Logo"] = (150, 150),
        ["Wide310x150Logo"] = (310, 150),
        ["Square310x310Logo"] = (310, 310),
    };

    // Unplated sizes the taskbar, Start and Explorer ask for.
    private static readonly int[] AppListTargetSizes = [16, 24, 32, 48, 256];

    private static readonly int[] AppIconSizes = [16, 24, 32, 48, 64, 128, 256];
    private static readonly int[] TrayIconSizes = [16, 20, 24, 32, 40, 48];

    [TestMethod]
    public void Manifest_references_only_images_with_a_known_size()
    {
        var unknown = ManifestImages().Select(i => i.Slot).Where(s => !BaseSizes.ContainsKey(s)).ToList();

        Assert.IsEmpty(unknown, "Add the base size of these manifest images: " + string.Join(", ", unknown));
    }

    [TestMethod]
    public void Every_manifest_image_has_a_scale_100_variant()
    {
        foreach (var image in ManifestImages())
        {
            var variants = Variants(image.Reference);
            Assert.IsTrue(variants.Any(v => v.Scale == 100 && v.TargetSize is null && v.AltForm is null),
                $"{image.Slot} ({image.Reference}) has no scale-100 file in {PackageImages}");
        }
    }

    [TestMethod]
    public void Every_manifest_image_variant_has_its_exact_pixel_size()
    {
        var wrong = new List<string>();
        foreach (var image in ManifestImages())
        {
            var (baseWidth, baseHeight) = BaseSizes[image.Slot];
            foreach (var variant in Variants(image.Reference))
            {
                var expected = variant.TargetSize is int target
                    ? (target, target)
                    : (Scale(baseWidth, variant.Scale), Scale(baseHeight, variant.Scale));
                var actual = ImageHeaders.PngSize(variant.Path);
                if (actual != expected)
                    wrong.Add($"{Path.GetFileName(variant.Path)}: expected {expected.Item1}x{expected.Item2}, got {actual.Width}x{actual.Height}");
            }
        }

        Assert.IsEmpty(wrong, string.Join(Environment.NewLine, wrong));
    }

    [TestMethod]
    public void Square44x44Logo_has_plated_and_unplated_target_sizes()
    {
        var reference = ManifestImages().Single(i => i.Slot == "Square44x44Logo").Reference;
        var variants = Variants(reference);

        foreach (var size in AppListTargetSizes)
        {
            Assert.IsTrue(variants.Any(v => v.TargetSize == size && v.AltForm is null), $"targetsize-{size} missing");
            Assert.IsTrue(variants.Any(v => v.TargetSize == size && v.AltForm == "unplated"),
                $"targetsize-{size}_altform-unplated missing");
        }
    }

    [TestMethod]
    public void Every_package_image_is_referenced_by_the_manifest()
    {
        var referenced = ManifestImages()
            .SelectMany(i => Variants(i.Reference))
            .Select(v => Path.GetFileName(v.Path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var orphans = Directory.EnumerateFiles(RepoRoot.File(PackageImages))
            .Select(Path.GetFileName)
            .Where(name => !referenced.Contains(name!))
            .ToList();

        Assert.IsEmpty(orphans, "Unreferenced package images: " + string.Join(", ", orphans));
    }

    [TestMethod]
    public void Package_images_live_in_one_tree()
    {
        var imageStems = ManifestImages()
            .Select(i => Path.GetFileNameWithoutExtension(i.Reference))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var packageImages = Path.GetFullPath(RepoRoot.File(PackageImages));

        var copies = PngFiles(new DirectoryInfo(RepoRoot.Path))
            .Where(f => !string.Equals(f.DirectoryName, packageImages, StringComparison.OrdinalIgnoreCase))
            .Where(f => imageStems.Contains(f.Name.Split('.')[0]))
            .Select(f => Path.GetRelativePath(RepoRoot.Path, f.FullName))
            .ToList();

        Assert.IsFalse(Directory.Exists(RepoRoot.File("TaskBard/Images")), "The app project's Images/ copy must be deleted.");
        Assert.IsEmpty(copies, "Package images outside " + PackageImages + ": " + string.Join(", ", copies));
    }

    [TestMethod]
    public void App_icon_is_TaskBard_ico_with_every_required_size()
    {
        var project = XDocument.Load(RepoRoot.File("TaskBard/TaskBard.csproj"));
        var applicationIcon = project.Descendants().SingleOrDefault(e => e.Name.LocalName == "ApplicationIcon")?.Value;

        Assert.AreEqual("TaskBard.ico", applicationIcon);
        AssertIcoSizes("TaskBard/TaskBard.ico", AppIconSizes);
    }

    [TestMethod]
    [DataRow("assets/tray/tray-light.ico")]
    [DataRow("assets/tray/tray-dark.ico")]
    [DataRow("assets/tray/tray-hc.ico")]
    public void Tray_icon_variant_has_every_required_size(string path)
    {
        AssertIcoSizes(path, TrayIconSizes);
    }

    private static void AssertIcoSizes(string relativePath, int[] required)
    {
        var path = RepoRoot.File(relativePath);
        Assert.IsTrue(File.Exists(path), $"{relativePath} is missing");

        var sizes = ImageHeaders.IcoFrameSizes(path);
        foreach (var size in required)
            Assert.IsTrue(sizes.Contains((size, size)), $"{relativePath} has no {size}x{size} frame");
    }

    private sealed record ManifestImage(string Slot, string Reference);

    private sealed record ImageVariant(string Path, int Scale, int? TargetSize, string? AltForm);

    /// <summary>Every PNG the manifest points at, keyed by the attribute or element that names it.</summary>
    private static List<ManifestImage> ManifestImages()
    {
        var manifest = XDocument.Load(RepoRoot.File(Manifest));
        var fromAttributes = manifest.Descendants().Attributes()
            .Where(a => a.Value.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            .Select(a => new ManifestImage(a.Name.LocalName, a.Value));
        var fromElements = manifest.Descendants()
            .Where(e => !e.HasElements && e.Value.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            .Select(e => new ManifestImage(e.Name.LocalName, e.Value));
        return fromAttributes.Concat(fromElements).ToList();
    }

    /// <summary>
    /// The files MRT can resolve a manifest reference to: the plain name, or
    /// the name with qualifiers such as <c>scale-200</c> or
    /// <c>targetsize-16_altform-unplated</c> before the extension.
    /// </summary>
    private static List<ImageVariant> Variants(string reference)
    {
        var relative = reference.Replace('\\', '/');
        var directory = RepoRoot.File(PackageDirectory + "/" + Path.GetDirectoryName(relative));
        var stem = Path.GetFileNameWithoutExtension(relative);
        var extension = Path.GetExtension(relative);

        if (!Directory.Exists(directory))
            return [];

        var variants = new List<ImageVariant>();
        foreach (var file in Directory.EnumerateFiles(directory, stem + "*" + extension))
        {
            var name = Path.GetFileName(file);
            if (name.Equals(stem + extension, StringComparison.OrdinalIgnoreCase))
            {
                variants.Add(new ImageVariant(file, 100, null, null));
                continue;
            }
            if (!name.StartsWith(stem + ".", StringComparison.OrdinalIgnoreCase))
                continue;

            var qualifiers = name[(stem.Length + 1)..^extension.Length].Split('_')
                .Select(q => q.Split('-', 2))
                .ToDictionary(q => q[0].ToLowerInvariant(), q => q.Length > 1 ? q[1] : "");
            variants.Add(new ImageVariant(
                file,
                qualifiers.TryGetValue("scale", out var scale) ? int.Parse(scale) : 100,
                qualifiers.TryGetValue("targetsize", out var target) ? int.Parse(target) : null,
                qualifiers.GetValueOrDefault("altform")));
        }
        return variants;
    }

    // Windows rounds scaled asset sizes half away from zero (e.g. 50 at 125% is 63).
    private static int Scale(int size, int scale) =>
        (int)Math.Round(size * scale / 100.0, MidpointRounding.AwayFromZero);

    private static IEnumerable<FileInfo> PngFiles(DirectoryInfo directory)
    {
        foreach (var file in directory.EnumerateFiles("*.png"))
            yield return file;
        foreach (var child in directory.EnumerateDirectories())
        {
            if (ProductSurfaceScanner.SkippedDirectories.Contains(child.Name))
                continue;
            foreach (var file in PngFiles(child))
                yield return file;
        }
    }
}
