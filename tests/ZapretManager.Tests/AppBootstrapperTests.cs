using ZapretManager.App.Services;

namespace ZapretManager.Tests;

public sealed class AppBootstrapperTests
{
    [Fact]
    public void Initialize_CreatesDirectoriesAndLoadsConfig()
    {
        var tempDir = Directory.CreateTempSubdirectory("zapret-bootstrap-test-");
        var configPath = Path.Combine(tempDir.FullName, "config.json");
        var layout = RuntimeLayout.ForDirectory(tempDir.FullName);
        var configService = new ConfigService(configPath);
        var bootstrapper = new AppBootstrapper(configService, layout);

        var result = bootstrapper.Initialize();
        var reloaded = configService.LoadOrCreate();

        Assert.True(Directory.Exists(layout.RuntimeDirectory));
        Assert.True(Directory.Exists(layout.TempDirectory));
        Assert.True(Directory.Exists(layout.BackupsDirectory));
        Assert.True(Directory.Exists(layout.LogsDirectory));
        Assert.NotNull(result.Config);
        Assert.NotNull(reloaded);
    }

    [Fact]
    public void Initialize_WhenSelectionIsMissing_SelectsAndSavesFirstAvailableStrategy()
    {
        var tempDir = Directory.CreateTempSubdirectory("zapret-bootstrap-test-");
        var configPath = Path.Combine(tempDir.FullName, "config.json");
        var layout = RuntimeLayout.ForDirectory(tempDir.FullName);
        layout.EnsureDirectories();
        File.WriteAllText(Path.Combine(layout.RuntimeDirectory, "general2.bat"), string.Empty);
        File.WriteAllText(Path.Combine(layout.RuntimeDirectory, "general.bat"), string.Empty);
        var configService = new ConfigService(configPath);

        var result = new AppBootstrapper(configService, layout).Initialize();

        Assert.Equal("general.bat", result.Config.SelectedStrategy);
        Assert.Equal("general.bat", configService.LoadOrCreate().SelectedStrategy);
    }

    [Fact]
    public void Initialize_WhenSavedStrategyNoLongerExists_SelectsValidFallback()
    {
        var tempDir = Directory.CreateTempSubdirectory("zapret-bootstrap-test-");
        var configPath = Path.Combine(tempDir.FullName, "config.json");
        var layout = RuntimeLayout.ForDirectory(tempDir.FullName);
        layout.EnsureDirectories();
        File.WriteAllText(Path.Combine(layout.RuntimeDirectory, "general.bat"), string.Empty);
        var configService = new ConfigService(configPath);
        configService.Save(new ZapretManager.App.Core.AppConfig { SelectedStrategy = "deleted.bat" });

        var result = new AppBootstrapper(configService, layout).Initialize();

        Assert.Equal("general.bat", result.Config.SelectedStrategy);
        Assert.Equal("general.bat", configService.LoadOrCreate().SelectedStrategy);
    }
}
