using ZapretManager.App.Services;

namespace ZapretManager.Tests;

public sealed class StrategyServiceTests
{
    [Fact]
    public void DiscoverStrategies_ReturnsOnlyGeneralBatFilesAndExcludesServiceBat()
    {
        var runtimeDirectory = Directory.CreateTempSubdirectory("zapret-strategy-test-").FullName;
        File.WriteAllText(Path.Combine(runtimeDirectory, "general.bat"), string.Empty);
        File.WriteAllText(Path.Combine(runtimeDirectory, "general (ALT).bat"), string.Empty);
        File.WriteAllText(Path.Combine(runtimeDirectory, "service.bat"), string.Empty);
        File.WriteAllText(Path.Combine(runtimeDirectory, "other.bat"), string.Empty);

        var strategies = StrategyService.DiscoverStrategies(runtimeDirectory);

        Assert.Equal(new[] { "general.bat", "general (ALT).bat" }, strategies.Select(strategy => strategy.FileName));
    }

    [Fact]
    public void DiscoverStrategies_UsesNaturalSortAndMarksSelectedStrategy()
    {
        var runtimeDirectory = Directory.CreateTempSubdirectory("zapret-strategy-test-").FullName;
        File.WriteAllText(Path.Combine(runtimeDirectory, "general (ALT10).bat"), string.Empty);
        File.WriteAllText(Path.Combine(runtimeDirectory, "general (ALT2).bat"), string.Empty);
        File.WriteAllText(Path.Combine(runtimeDirectory, "general.bat"), string.Empty);

        var strategies = StrategyService.DiscoverStrategies(runtimeDirectory, selectedStrategy: "general (ALT2).bat");

        Assert.Equal(new[] { "general.bat", "general (ALT2).bat", "general (ALT10).bat" }, strategies.Select(strategy => strategy.FileName));
        Assert.True(strategies.Single(strategy => strategy.FileName == "general (ALT2).bat").IsSelected);
        Assert.False(strategies.Single(strategy => strategy.FileName == "general.bat").IsSelected);
    }
}
