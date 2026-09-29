using ZapretManager.App.Core;
using ZapretManager.App.UI;

namespace ZapretManager.Tests;

/// <summary>Общая заглушка команд UI: запросы настраиваются провайдерами, команды пишутся в Calls.</summary>
internal sealed class FakeZapretManagerCommands : IZapretManagerCommands
{
    public Func<ZapretState> StateProvider { get; set; } = () => ZapretState.Stopped;

    public Func<IReadOnlyList<StrategyInfo>> StrategiesProvider { get; set; } = () => Array.Empty<StrategyInfo>();

    public Func<LastStrategyScanResult?> LastStrategyScanProvider { get; set; } = () => null;

    public Func<string> StatusHintProvider { get; set; } = () => string.Empty;

    public Func<string> RuntimeVersionTextProvider { get; set; } = () => string.Empty;

    public Action<StrategyInfo>? StrategySelectedHook { get; set; }

    public List<string> Calls { get; } = [];

    public ZapretState GetState() => StateProvider();

    public IReadOnlyList<StrategyInfo> GetStrategies() => StrategiesProvider();

    public LastStrategyScanResult? GetLastStrategyScan() => LastStrategyScanProvider();

    public string GetStatusHint() => StatusHintProvider();

    public string GetRuntimeVersionText() => RuntimeVersionTextProvider();

    public void SelectStrategy(StrategyInfo strategy)
    {
        Calls.Add("SelectStrategy:" + strategy.FileName);
        StrategySelectedHook?.Invoke(strategy);
    }

    public void StartZapret() => Calls.Add("StartZapret");

    public void StopZapret() => Calls.Add("StopZapret");

    public void RestartZapret() => Calls.Add("RestartZapret");

    public void StopExistingZapret() => Calls.Add("StopExistingZapret");

    public void CheckUpdates() => Calls.Add("CheckUpdates");

    public void RunStrategyAutoSelection() => Calls.Add("RunStrategyAutoSelection");

    public void CancelStrategyAutoSelection() => Calls.Add("CancelStrategyAutoSelection");

    public void ClearDiscordCache() => Calls.Add("ClearDiscordCache");

    public void OpenSettings() => Calls.Add("OpenSettings");

    public void ShowMainWindow() => Calls.Add("ShowMainWindow");

    public void ExitApplication() => Calls.Add("ExitApplication");
}
