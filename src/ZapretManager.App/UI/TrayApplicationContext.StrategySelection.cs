using ZapretManager.App.Core;
using ZapretManager.App.Services;

namespace ZapretManager.App.UI;

public sealed partial class TrayApplicationContext
{
    public async void SelectStrategy(StrategyInfo strategy)
    {
        if (_exitInProgress || string.Equals(_config.SelectedStrategy, strategy.FileName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!await _zapretActionGate.WaitAsync(0))
        {
            _notifications.ShowInformation("Другое действие с zapret уже выполняется.");
            RefreshUi();
            return;
        }

        _mainForm.SetStrategySelectionEnabled(false);
        try
        {
            if (!IsManagedProcessRunning())
            {
                // Выбор виден сразу в списке и в меню трея — отдельное уведомление было бы шумом.
                StrategySelectionService.SelectStrategy(_configService, _config, strategy);
                _logger.Info("Strategy selected: " + strategy.FileName);
                return;
            }

            var switchService = new StrategySwitchService(
                _configService,
                _config,
                action => ExecuteZapretActionAsync(action),
                IsManagedProcessRunning);
            var result = await switchService.SwitchAsync(strategy);
            if (result.IsSuccess)
            {
                _logger.Info($"Strategy changed and restarted: {strategy.FileName}");
                ShowFeedback($"Запущена стратегия: {strategy.DisplayName}");
                return;
            }

            var restoreMessage = result.RestoreResponse?.Message ?? "восстановление не выполнялось";
            _logger.Info($"Strategy change failed: {result.SwitchResponse.Message}. Restore: {restoreMessage}");
            ThemedMessageBox.Show(
                result.PreviousStrategyRestored
                    ? result.SwitchResponse.Message + "\n\nПредыдущая стратегия восстановлена."
                    : result.SwitchResponse.Message + "\n\nНе удалось восстановить предыдущую стратегию: " + restoreMessage,
                "Zapret Manager",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            _logger.Error("Strategy change failed.", ex);
            ThemedMessageBox.Show(
                "Не удалось переключить стратегию.\n\n" + ex.Message,
                "Zapret Manager",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        finally
        {
            _mainForm.SetStrategySelectionEnabled(true);
            RefreshUi();
            _zapretActionGate.Release();
        }
    }
}
