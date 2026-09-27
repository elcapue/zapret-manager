using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using ZapretManager.App.Core;
using ZapretManager.App.Services;

namespace ZapretManager.Tests;

public sealed class UpdateServiceTests
{
    [Fact]
    public async Task ApplyUpdateAsync_DownloadsVerifiesExtractsAndReplacesRuntime()
    {
        var baseDir = Directory.CreateTempSubdirectory("zapret-update-test-").FullName;
        var layout = RuntimeLayout.ForDirectory(baseDir);
        layout.EnsureDirectories();
        File.WriteAllText(Path.Combine(layout.RuntimeDirectory, "old.txt"), "old runtime");
        var zipBytes = CreateZapretReleaseZip("zapret-discord-youtube-1.10.2");
        var asset = new ReleaseAssetInfo("zapret-discord-youtube-1.10.2.zip", "https://example.test/update.zip", "sha256:" + Sha256Hex(zipBytes));
        var release = new GitHubReleaseInfo("1.10.2", "https://example.test/release", asset);
        var config = new AppConfig { LastKnownVersion = "1.10.1" };
        var service = new UpdateService(new HttpClient(new BytesHttpHandler(zipBytes)), layout);

        var result = await service.ApplyUpdateAsync(release, config, CancellationToken.None);

        Assert.Equal(UpdateApplyStatus.Applied, result.Status);
        Assert.Equal("1.10.2", config.LastKnownVersion);
        Assert.True(File.Exists(Path.Combine(layout.RuntimeDirectory, "bin", "winws.exe")));
        Assert.True(File.Exists(Path.Combine(layout.RuntimeDirectory, "general.bat")));
        Assert.False(File.Exists(Path.Combine(layout.RuntimeDirectory, "old.txt")));
        Assert.True(Directory.Exists(Path.Combine(layout.BackupsDirectory, "runtime-previous")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(layout.TempDirectory));
    }

    [Fact]
    public async Task ApplyUpdateAsync_WhenDigestDoesNotMatch_DoesNotReplaceRuntime()
    {
        var baseDir = Directory.CreateTempSubdirectory("zapret-update-test-").FullName;
        var layout = RuntimeLayout.ForDirectory(baseDir);
        layout.EnsureDirectories();
        File.WriteAllText(Path.Combine(layout.RuntimeDirectory, "old.txt"), "old runtime");
        var zipBytes = CreateZapretReleaseZip("zapret-discord-youtube-1.10.2");
        var asset = new ReleaseAssetInfo("zapret-discord-youtube-1.10.2.zip", "https://example.test/update.zip", "sha256:bad");
        var release = new GitHubReleaseInfo("1.10.2", "https://example.test/release", asset);
        var config = new AppConfig { LastKnownVersion = "1.10.1" };
        var service = new UpdateService(new HttpClient(new BytesHttpHandler(zipBytes)), layout);

        var result = await service.ApplyUpdateAsync(release, config, CancellationToken.None);

        Assert.Equal(UpdateApplyStatus.HashMismatch, result.Status);
        Assert.Equal("1.10.1", config.LastKnownVersion);
        Assert.True(File.Exists(Path.Combine(layout.RuntimeDirectory, "old.txt")));
        Assert.False(File.Exists(Path.Combine(layout.RuntimeDirectory, "bin", "winws.exe")));
    }

    [Fact]
    public async Task PrepareUpdateAsync_DownloadsAndValidatesWithoutTouchingCurrentRuntime()
    {
        var baseDir = Directory.CreateTempSubdirectory("zapret-update-test-").FullName;
        var layout = RuntimeLayout.ForDirectory(baseDir);
        layout.EnsureDirectories();
        File.WriteAllText(Path.Combine(layout.RuntimeDirectory, "old.txt"), "old runtime");
        var zipBytes = CreateZapretReleaseZip("zapret-discord-youtube-1.10.2");
        var asset = new ReleaseAssetInfo(
            "zapret-discord-youtube-1.10.2.zip",
            "https://example.test/update.zip",
            "sha256:" + Sha256Hex(zipBytes));
        var release = new GitHubReleaseInfo("1.10.2", "https://example.test/release", asset);
        var service = new UpdateService(new HttpClient(new BytesHttpHandler(zipBytes)), layout);

        var result = await service.PrepareUpdateAsync(release, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.PreparedUpdate);
        Assert.True(File.Exists(Path.Combine(layout.RuntimeDirectory, "old.txt")));
        Assert.False(File.Exists(Path.Combine(layout.RuntimeDirectory, "bin", "winws.exe")));
        Assert.NotEmpty(Directory.EnumerateFileSystemEntries(layout.TempDirectory));

        result.PreparedUpdate.Dispose();
        Assert.Empty(Directory.EnumerateFileSystemEntries(layout.TempDirectory));
    }

    [Fact]
    public async Task ApplyUpdateAsync_PreservesUserFilesDuringRuntimeSwap()
    {
        var baseDir = Directory.CreateTempSubdirectory("zapret-update-test-").FullName;
        var layout = RuntimeLayout.ForDirectory(baseDir);
        layout.EnsureDirectories();
        Directory.CreateDirectory(Path.Combine(layout.RuntimeDirectory, "lists"));
        File.WriteAllText(Path.Combine(layout.RuntimeDirectory, "lists", "list-general-user.txt"), "user-domain.example");
        var zipBytes = CreateZapretReleaseZip("zapret-discord-youtube-1.10.2");
        var asset = new ReleaseAssetInfo("zapret-discord-youtube-1.10.2.zip", "https://example.test/update.zip", "sha256:" + Sha256Hex(zipBytes));
        var release = new GitHubReleaseInfo("1.10.2", "https://example.test/release", asset);
        var config = new AppConfig { LastKnownVersion = "1.10.1" };
        var service = new UpdateService(new HttpClient(new BytesHttpHandler(zipBytes)), layout);

        var result = await service.ApplyUpdateAsync(release, config, CancellationToken.None);

        Assert.Equal(UpdateApplyStatus.Applied, result.Status);
        Assert.Equal("user-domain.example", File.ReadAllText(Path.Combine(layout.RuntimeDirectory, "lists", "list-general-user.txt")));
    }

    [Theory]
    [InlineData("old-list", "fresh-list", null)]
    [InlineData("", "", "fresh-list")]
    [InlineData("203.0.113.113/32\r\n", "203.0.113.113/32\r\n", "fresh-list")]
    public async Task ApplyUpdateAsync_KeepsUserIpsetModeWithFreshListFromRelease(
        string userIpsetAll,
        string expectedIpsetAll,
        string? expectedBackup)
    {
        // Релиз Flowseal поставляется в режиме «none»: заглушка в ipset-all.txt, список — в .backup.
        var baseDir = Directory.CreateTempSubdirectory("zapret-update-test-").FullName;
        var layout = RuntimeLayout.ForDirectory(baseDir);
        layout.EnsureDirectories();
        var lists = Path.Combine(layout.RuntimeDirectory, "lists");
        Directory.CreateDirectory(lists);
        File.WriteAllText(Path.Combine(lists, "ipset-all.txt"), userIpsetAll);
        File.WriteAllText(Path.Combine(lists, "list-general-user.txt"), "custom-user-list");
        var zipBytes = CreateZapretReleaseZip(
            "zapret-discord-youtube-1.10.2",
            ipsetAllContent: "203.0.113.113/32\r\n",
            ipsetBackupContent: "fresh-list");
        var asset = new ReleaseAssetInfo("zapret-discord-youtube-1.10.2.zip", "https://example.test/update.zip", "sha256:" + Sha256Hex(zipBytes));
        var release = new GitHubReleaseInfo("1.10.2", "https://example.test/release", asset);
        var service = new UpdateService(new HttpClient(new BytesHttpHandler(zipBytes)), layout);

        var result = await service.ApplyUpdateAsync(release, new AppConfig(), CancellationToken.None);

        Assert.Equal(UpdateApplyStatus.Applied, result.Status);
        Assert.Equal(expectedIpsetAll, File.ReadAllText(Path.Combine(lists, "ipset-all.txt")));
        var backupPath = Path.Combine(lists, "ipset-all.txt.backup");
        Assert.Equal(expectedBackup, File.Exists(backupPath) ? File.ReadAllText(backupPath) : null);
        Assert.Equal("custom-user-list", File.ReadAllText(Path.Combine(lists, "list-general-user.txt")));
    }

    [Fact]
    public async Task ApplyUpdateAsync_OnFirstInstall_KeepsReleaseIpsetFilesAsIs()
    {
        var baseDir = Directory.CreateTempSubdirectory("zapret-update-test-").FullName;
        var layout = RuntimeLayout.ForDirectory(baseDir);
        var zipBytes = CreateZapretReleaseZip(
            "zapret-discord-youtube-1.10.2",
            ipsetAllContent: "203.0.113.113/32\r\n",
            ipsetBackupContent: "fresh-list");
        var asset = new ReleaseAssetInfo("zapret-discord-youtube-1.10.2.zip", "https://example.test/update.zip", "sha256:" + Sha256Hex(zipBytes));
        var release = new GitHubReleaseInfo("1.10.2", "https://example.test/release", asset);
        var service = new UpdateService(new HttpClient(new BytesHttpHandler(zipBytes)), layout);

        var result = await service.ApplyUpdateAsync(release, new AppConfig(), CancellationToken.None);

        Assert.Equal(UpdateApplyStatus.Applied, result.Status);
        var lists = Path.Combine(layout.RuntimeDirectory, "lists");
        Assert.Equal("203.0.113.113/32\r\n", File.ReadAllText(Path.Combine(lists, "ipset-all.txt")));
        Assert.Equal("fresh-list", File.ReadAllText(Path.Combine(lists, "ipset-all.txt.backup")));
    }

    [Fact]
    public async Task ApplyUpdateAsync_AfterRepeatedSuccess_KeepsOneBackupAndCleansTemp()
    {
        var baseDir = Directory.CreateTempSubdirectory("zapret-update-test-").FullName;
        var layout = RuntimeLayout.ForDirectory(baseDir);
        layout.EnsureDirectories();
        var zipBytes = CreateZapretReleaseZip("zapret-discord-youtube");
        var asset = new ReleaseAssetInfo(
            "zapret.zip",
            "https://example.test/update.zip",
            "sha256:" + Sha256Hex(zipBytes));
        var service = new UpdateService(new HttpClient(new BytesHttpHandler(zipBytes)), layout);
        var config = new AppConfig();

        var first = await service.ApplyUpdateAsync(
            new GitHubReleaseInfo("1.0.0", "https://example.test/1.0.0", asset),
            config,
            CancellationToken.None);
        File.WriteAllText(Path.Combine(layout.RuntimeDirectory, "version-marker.txt"), "1.0.0");
        var second = await service.ApplyUpdateAsync(
            new GitHubReleaseInfo("1.1.0", "https://example.test/1.1.0", asset),
            config,
            CancellationToken.None);

        Assert.Equal(UpdateApplyStatus.Applied, first.Status);
        Assert.Equal(UpdateApplyStatus.Applied, second.Status);
        var backups = Directory.GetDirectories(layout.BackupsDirectory);
        var backup = Assert.Single(backups);
        Assert.Equal("runtime-previous", Path.GetFileName(backup));
        Assert.True(File.Exists(Path.Combine(backup, "version-marker.txt")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(layout.TempDirectory));
    }

    private static byte[] CreateZapretReleaseZip(
        string rootFolder,
        string? ipsetAllContent = null,
        string? ipsetBackupContent = null)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(archive, $"{rootFolder}/bin/winws.exe", "winws");
            AddEntry(archive, $"{rootFolder}/bin/WinDivert64.sys", "driver");
            AddEntry(archive, $"{rootFolder}/bin/WinDivert.dll", "dll");
            AddEntry(archive, $"{rootFolder}/service.bat", "service");
            AddEntry(archive, $"{rootFolder}/general.bat", "general");
            if (ipsetAllContent is not null)
            {
                AddEntry(archive, $"{rootFolder}/lists/ipset-all.txt", ipsetAllContent);
            }

            if (ipsetBackupContent is not null)
            {
                AddEntry(archive, $"{rootFolder}/lists/ipset-all.txt.backup", ipsetBackupContent);
            }
        }

        return stream.ToArray();
    }

    private static void AddEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }

    private static string Sha256Hex(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private sealed class BytesHttpHandler : HttpMessageHandler
    {
        private readonly byte[] _bytes;

        public BytesHttpHandler(byte[] bytes)
        {
            _bytes = bytes;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(_bytes)
            });
        }
    }
}
