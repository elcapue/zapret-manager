using ZapretManager.App.Core;
using ZapretManager.App.Services;

namespace ZapretManager.App.UI;

/// <summary>Команды главного окна и меню трея поверх приватных обработчиков контекста.</summary>
public sealed partial class TrayApplicationContext
{
    public ZapretState GetState() => _state;

    public LastStrategyScanResult? GetLastStrategyScan() => _config.LastStrategyScan;

    public void StartZapret() => RunZapretAction(ZapretActionKind.Start);

    public void StopZapret() => RunZapretAction(ZapretActionKind.Stop);

    public void RestartZapret() => RunZapretAction(ZapretActionKind.Restart);

    public void StopExistingZapret() => StopExistingZapretWithConfirmation(requireConfirmation: true);

    public void CheckUpdates() => _ = CheckUpdatesAsync();

    public void RunStrategyAutoSelection() => RunStrategyTests();

    public void CancelStrategyAutoSelection() => CancelStrategyTests();
}
