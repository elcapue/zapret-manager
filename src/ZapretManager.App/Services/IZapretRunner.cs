using ZapretManager.App.Core;

namespace ZapretManager.App.Services;

public interface IZapretRunner
{
    ZapretRunResult Start(StrategyInfo? strategy);

    ZapretRunResult Stop();

    ZapretRunResult Restart(StrategyInfo? strategy);
}
