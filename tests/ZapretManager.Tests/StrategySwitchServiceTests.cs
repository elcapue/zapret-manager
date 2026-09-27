using ZapretManager.App.Core;
using ZapretManager.App.Services;

namespace ZapretManager.Tests;

public sealed class StrategySwitchServiceTests
{
    [Fact]
    public async Task SwitchAsync_WhenRestartSucceeds_KeepsNewStrategy()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("zapret-switch-test-");
        var configService = new ConfigService(Path.Combine(tempDirectory.FullName, "config.json"));
        var config = new AppConfig { SelectedStrategy = "general.bat" };
        configService.Save(config);
        var calls = new List<ZapretActionKind>();
        var service = new StrategySwitchService(
            configService,
            config,
            action =>
            {
                calls.Add(action);
                return Task.FromResult(new ZapretActionResponse(ZapretActionOutcome.Succeeded, "ok"));
            },
            () => false);

        var result = await service.SwitchAsync(
            new StrategyInfo("general (ALT).bat", "C:/runtime/general (ALT).bat", false));

        Assert.True(result.IsSuccess);
        Assert.Equal([ZapretActionKind.Restart], calls);
        Assert.Equal("general (ALT).bat", configService.LoadOrCreate().SelectedStrategy);
    }

    [Fact]
    public async Task SwitchAsync_WhenRestartFails_RestoresPreviousStrategy()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("zapret-switch-test-");
        var configService = new ConfigService(Path.Combine(tempDirectory.FullName, "config.json"));
        var config = new AppConfig { SelectedStrategy = "general.bat" };
        configService.Save(config);
        var responses = new Queue<ZapretActionResponse>(
        [
            new ZapretActionResponse(ZapretActionOutcome.Failed, "new failed"),
            new ZapretActionResponse(ZapretActionOutcome.Succeeded, "old restored")
        ]);
        var calls = new List<ZapretActionKind>();
        var service = new StrategySwitchService(
            configService,
            config,
            action =>
            {
                calls.Add(action);
                return Task.FromResult(responses.Dequeue());
            },
            () => false);

        var result = await service.SwitchAsync(
            new StrategyInfo("general (ALT).bat", "C:/runtime/general (ALT).bat", false));

        Assert.False(result.IsSuccess);
        Assert.True(result.PreviousStrategyRestored);
        Assert.Equal([ZapretActionKind.Restart, ZapretActionKind.Start], calls);
        Assert.Equal("general.bat", configService.LoadOrCreate().SelectedStrategy);
    }
}
