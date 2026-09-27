using ZapretManager.App.UI;

namespace ZapretManager.Tests;

public sealed class AppAssetsTests
{
    [Fact]
    public void GetBrandingResourceName_ReturnsEmbeddedResourceName()
    {
        var name = AppAssets.GetBrandingResourceName("zapret-manager.ico");

        Assert.Equal("ZapretManager.App.Assets.Branding.zapret-manager.ico", name);
    }

    [Fact]
    public void SourceBrandingAssets_ExistInProjectAssetsFolder()
    {
        var projectRoot = GetProjectRoot();
        var branding = Path.Combine(projectRoot, "assets", "branding");

        Assert.True(File.Exists(Path.Combine(branding, "zapret-manager.ico")));
        Assert.True(File.Exists(Path.Combine(branding, "zapret-manager-tray-32.png")));
        Assert.True(File.Exists(Path.Combine(branding, AppAssets.MarkFileName)));
    }

    [Fact]
    public void EmbeddedBrandingAssets_AreAvailableFromAssembly()
    {
        var resources = typeof(AppAssets).Assembly.GetManifestResourceNames();

        Assert.Contains(AppAssets.GetBrandingResourceName("zapret-manager.ico"), resources);
        Assert.Contains(AppAssets.GetBrandingResourceName("zapret-manager-tray-32.png"), resources);
        Assert.Contains(AppAssets.GetBrandingResourceName(AppAssets.MarkFileName), resources);
    }

    [Fact]
    public void LoadMark_ReturnsIndependentUsableBitmaps()
    {
        using var first = AppAssets.LoadMark();
        using var second = AppAssets.LoadMark();

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotSame(first, second);
        Assert.Equal(first!.Size, second!.Size);
        _ = first.GetPixel(0, 0);
        _ = second.GetPixel(0, 0);
    }

    private static string GetProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ZapretManager.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Project root not found.");
    }
}
