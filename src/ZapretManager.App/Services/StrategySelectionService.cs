using ZapretManager.App.Core;

namespace ZapretManager.App.Services;

public static class StrategySelectionService
{
    public static void SelectStrategy(ConfigService configService, AppConfig config, StrategyInfo strategy)
    {
        config.SelectedStrategy = strategy.FileName;
        configService.Save(config);
    }
}
