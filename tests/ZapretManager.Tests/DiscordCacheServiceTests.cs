using ZapretManager.App.Services;

namespace ZapretManager.Tests;

public sealed class DiscordCacheServiceTests
{
    [Fact]
    public void Clear_ClosesInstalledClientsAndDeletesOnlyCacheFolders()
    {
        var appData = Directory.CreateTempSubdirectory("zapret-discord-test-").FullName;
        var stable = CreateInstallation(appData, "discord");
        var canary = CreateInstallation(appData, "discordcanary");
        var closed = new List<string>();
        var service = new DiscordCacheService(appData, name => { closed.Add(name); return true; }, TimeSpan.Zero);

        var result = service.Clear();

        Assert.True(result.IsSuccess);
        Assert.Equal(["Discord", "Discord Canary"], result.Cleared);
        Assert.Equal(["Discord", "DiscordCanary"], closed);
        foreach (var dataDirectory in new[] { stable, canary })
        {
            Assert.False(Directory.Exists(Path.Combine(dataDirectory, "Cache")));
            Assert.False(Directory.Exists(Path.Combine(dataDirectory, "Code Cache")));
            Assert.False(Directory.Exists(Path.Combine(dataDirectory, "GPUCache")));
            Assert.True(File.Exists(Path.Combine(dataDirectory, "settings.json")));
            Assert.True(Directory.Exists(Path.Combine(dataDirectory, "Local Storage")));
        }
    }

    [Fact]
    public void Clear_WhenClientCannotBeClosed_KeepsItsCacheAndReportsFailure()
    {
        var appData = Directory.CreateTempSubdirectory("zapret-discord-test-").FullName;
        var stable = CreateInstallation(appData, "discord");
        var service = new DiscordCacheService(appData, _ => false, TimeSpan.Zero);

        var result = service.Clear();

        Assert.False(result.IsSuccess);
        Assert.Contains("не удалось закрыть", result.Message);
        Assert.True(Directory.Exists(Path.Combine(stable, "Cache")));
    }

    [Fact]
    public void FindInstalled_WhenDiscordIsMissing_ReturnsEmptyAndClearDoesNothing()
    {
        var appData = Directory.CreateTempSubdirectory("zapret-discord-test-").FullName;
        var service = new DiscordCacheService(appData, _ => throw new InvalidOperationException(), TimeSpan.Zero);

        Assert.Empty(service.FindInstalled());
        var result = service.Clear();
        Assert.False(result.FoundAny);
        Assert.Equal("Discord не найден.", result.Message);
    }

    private static string CreateInstallation(string appData, string folder)
    {
        var dataDirectory = Path.Combine(appData, folder);
        foreach (var cacheFolder in new[] { "Cache", "Code Cache", "GPUCache" })
        {
            Directory.CreateDirectory(Path.Combine(dataDirectory, cacheFolder, "nested"));
            File.WriteAllText(Path.Combine(dataDirectory, cacheFolder, "nested", "data_0"), "cache");
        }

        Directory.CreateDirectory(Path.Combine(dataDirectory, "Local Storage"));
        File.WriteAllText(Path.Combine(dataDirectory, "settings.json"), "{}");
        return dataDirectory;
    }
}
