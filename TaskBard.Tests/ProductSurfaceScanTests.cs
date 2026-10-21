namespace TaskBard.Tests;

[TestClass]
public sealed class ProductSurfaceScanTests
{
    [TestMethod]
    public void Active_product_surfaces_use_only_the_TaskBard_name_and_repo()
    {
        var findings = ProductSurfaceScanner.ScanRepository(RepoRoot.Path);

        Assert.IsEmpty(findings,
            "Abandoned names or stale repo URLs found in active product surfaces. " +
            "Fix them, or add a historical record to the allow-list in ProductSurfaceScanner:\n" +
            string.Join("\n", findings));
    }
}
