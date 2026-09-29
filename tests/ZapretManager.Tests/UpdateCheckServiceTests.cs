using ZapretManager.App.Core;
using ZapretManager.App.Services;

namespace ZapretManager.Tests;

public sealed class UpdateCheckServiceTests
{
    [Theory]
    [InlineData("1.10.1", "1.10.2")]
    [InlineData("1.9.7", "1.10.0")]
    [InlineData("1.9.7", "1.9.7b")]
    [InlineData("1.10", "1.10.1")]
    [InlineData("custom-build", "1.10.2")]
    public void Compare_WhenReleaseIsNewer_ReturnsUpdateAvailable(string current, string latest)
    {
        var asset = new ReleaseAssetInfo($"zapret-discord-youtube-{latest}.zip", "https://example.test/update.zip", "sha256:abc");
        var release = new GitHubReleaseInfo(latest, asset);

        var result = UpdateCheckService.Compare(current, release);

        Assert.Equal(UpdateAvailability.UpdateAvailable, result.Availability);
        Assert.Equal(current, result.CurrentVersion);
        Assert.Equal(latest, result.LatestVersion);
        Assert.Same(asset, result.ZipAsset);
    }

    [Theory]
    [InlineData("1.10.1", "1.10.1")]
    [InlineData("v1.10.1", "1.10.1")]
    [InlineData("1.10.1", "1.10.1.0")]
    [InlineData("1.10.3", "1.10.2")]
    [InlineData("1.9.7b", "1.9.7")]
    public void Compare_WhenInstalledIsSameOrNewer_ReturnsUpToDate(string current, string latest)
    {
        // Более новая установленная версия бывает, когда последний релиз отозвали: предлагать откат нельзя.
        var release = new GitHubReleaseInfo(latest, null);

        var result = UpdateCheckService.Compare(current, release);

        Assert.Equal(UpdateAvailability.UpToDate, result.Availability);
    }

    [Fact]
    public void Compare_WhenCurrentVersionIsUnknown_ReturnsUnknownCurrentVersion()
    {
        var release = new GitHubReleaseInfo("1.10.2", null);

        var result = UpdateCheckService.Compare(currentVersion: null, release);

        Assert.Equal(UpdateAvailability.UnknownCurrentVersion, result.Availability);
        Assert.Equal("неизвестна", result.CurrentVersion);
        Assert.Equal("1.10.2", result.LatestVersion);
    }
}
