using System.Net;
using System.Security.Cryptography;
using ZapretManager.App.Core;
using ZapretManager.App.Services;

namespace ZapretManager.Tests;

public sealed class ManagerUpdateServiceTests
{
    [Fact]
    public void Evaluate_WhenReleaseIsNewer_PicksWindowsExecutableAsset()
    {
        var exe = Asset("ZapretManager-0.2.0-win-x64.exe");
        var release = Release("v0.2.0", Asset("source.zip"), exe);

        var check = ManagerUpdateService.Evaluate(release, "0.1.0");

        Assert.True(check.IsAvailable);
        Assert.Equal("0.2.0", check.LatestVersion);
        Assert.Same(exe, check.Asset);
    }

    [Theory]
    [InlineData("0.1.0")]
    [InlineData("0.0.9")]
    public void Evaluate_WhenReleaseIsNotNewer_OffersNothing(string latest)
    {
        var release = Release(latest, Asset($"ZapretManager-{latest}-win-x64.exe"));

        Assert.False(ManagerUpdateService.Evaluate(release, "0.1.0").IsAvailable);
    }

    [Fact]
    public void Evaluate_WhenReleaseHasNoExecutable_OffersNothing()
    {
        var release = Release("0.2.0", Asset("ZapretManager-0.2.0-win-x64.zip"));

        Assert.False(ManagerUpdateService.Evaluate(release, "0.1.0").IsAvailable);
    }

    [Fact]
    public async Task DownloadAsync_WhenDigestMatches_SavesFile()
    {
        var bytes = "new manager"u8.ToArray();
        var directory = Directory.CreateTempSubdirectory("zapret-manager-update-").FullName;
        var service = new ManagerUpdateService(new HttpClient(new BytesHandler(bytes)));

        var path = await service.DownloadAsync(Asset("ZapretManager-0.2.0-win-x64.exe", Sha256(bytes)), directory, CancellationToken.None);

        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("sha256:0000000000000000000000000000000000000000000000000000000000000000")]
    public async Task DownloadAsync_WithoutMatchingDigest_RefusesAndSavesNothing(string? digest)
    {
        var directory = Directory.CreateTempSubdirectory("zapret-manager-update-").FullName;
        var service = new ManagerUpdateService(new HttpClient(new BytesHandler("tampered"u8.ToArray())));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.DownloadAsync(Asset("ZapretManager-0.2.0-win-x64.exe", digest), directory, CancellationToken.None));

        Assert.Empty(Directory.GetFiles(directory));
    }

    private static ReleaseAssetInfo Asset(string name, string? digest = "sha256:abc")
    {
        return new ReleaseAssetInfo(name, "https://example.test/" + name, digest);
    }

    private static GitHubReleaseInfo Release(string tag, params ReleaseAssetInfo[] assets)
    {
        return new GitHubReleaseInfo(tag, "https://example.test/release", zipAsset: null, assets);
    }

    private static string Sha256(byte[] bytes)
    {
        return "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private sealed class BytesHandler(byte[] bytes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
        }
    }
}
