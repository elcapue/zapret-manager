using ZapretManager.App.Core;
using ZapretManager.App.Services;

namespace ZapretManager.Tests;

public sealed class StrategySelectionServiceTests
{
    [Fact]
    public void SelectStrategy_SavesSelectedStrategyFileName()
    {
        var tempDir = Directory.CreateTempSubdirectory("zapret-strategy-selection-test-");
        var configPath = Path.Combine(tempDir.FullName, "config.json");
        var configService = new ConfigService(configPath);
        var config = new AppConfig();
        var strategy = new StrategyInfo("general (ALT).bat", Path.Combine(tempDir.FullName, "general (ALT).bat"), isSelected: false);

        StrategySelectionService.SelectStrategy(configService, config, strategy);
        var reloaded = configService.LoadOrCreate();

        Assert.Equal("general (ALT).bat", config.SelectedStrategy);
        Assert.Equal("general (ALT).bat", reloaded.SelectedStrategy);
    }
}
