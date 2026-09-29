using ZapretManager.App.Core;

namespace ZapretManager.App.UI;

/// <summary>
/// Единый набор команд и запросов для главного окна и меню трея — оба потребляют его
/// вместо собственных списков делегатов. Реализует <see cref="TrayApplicationContext"/>.
/// </summary>
public interface IZapretManagerCommands
{
    ZapretState GetState();

    IReadOnlyList<StrategyInfo> GetStrategies();

    LastStrategyScanResult? GetLastStrategyScan();

    string GetStatusHint();

    string GetRuntimeVersionText();

    void SelectStrategy(StrategyInfo strategy);

    void StartZapret();

    void StopZapret();

    void RestartZapret();

    void StopExistingZapret();

    void CheckUpdates();

    void RunStrategyAutoSelection();

    void CancelStrategyAutoSelection();

    void ClearDiscordCache();

    void OpenSettings();

    void ShowMainWindow();

    void ExitApplication();
}
