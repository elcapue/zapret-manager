using ZapretManager.App.Core;

namespace ZapretManager.App.Services;

public sealed class AppBootstrapResult
{
    public AppBootstrapResult(AppConfig config, RuntimeLayout runtimeLayout)
    {
        Config = config;
        RuntimeLayout = runtimeLayout;
    }

    public AppConfig Config { get; }

    public RuntimeLayout RuntimeLayout { get; }
}

public sealed class AppBootstrapper
{
    private readonly ConfigService _configService;
    private readonly RuntimeLayout _runtimeLayout;

    public AppBootstrapper(ConfigService configService, RuntimeLayout runtimeLayout)
    {
        _configService = configService;
        _runtimeLayout = runtimeLayout;
    }

    public AppBootstrapResult Initialize()
    {
        _runtimeLayout.EnsureDirectories();
        var config = _configService.LoadOrCreate();
        var strategies = StrategyService.DiscoverStrategies(
            _runtimeLayout.RuntimeDirectory,
            config.SelectedStrategy);
        if (strategies.Count > 0 && strategies.All(strategy => !strategy.IsSelected))
        {
            StrategySelectionService.SelectStrategy(_configService, config, strategies[0]);
        }

        return new AppBootstrapResult(config, _runtimeLayout);
    }
}
