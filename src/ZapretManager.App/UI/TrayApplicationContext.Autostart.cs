using ZapretManager.App.Services;

namespace ZapretManager.App.UI;

public sealed partial class TrayApplicationContext
{
    private void ShowMainWindow()
    {
        if (!EnsureRuntimeAvailableForInteractiveUse())
        {
            return;
        }

        _mainForm.Show();
        _mainForm.WindowState = FormWindowState.Normal;
        _mainForm.Activate();
        RefreshUi();
    }

    private bool EnsureRuntimeAvailableForInteractiveUse()
    {
        var bootstrapService = new RuntimeBootstrapService(_runtimeLayout);
        if (!bootstrapService.ShouldOfferBootstrap())
        {
            return true;
        }

        if (_runtimePromptOpen)
        {
            return false;
        }

        _runtimePromptOpen = true;
        try
        {
            using var bootstrapForm = new RuntimeBootstrapForm(bootstrapService, _configService, _config);
            return bootstrapForm.ShowDialog() == DialogResult.OK;
        }
        finally
        {
            _runtimePromptOpen = false;
        }
    }

    private void ScheduleAutostartStrategy()
    {
        _autostartTimer = new System.Windows.Forms.Timer { Interval = 1_000 };
        _autostartTimer.Tick += async (_, _) =>
        {
            _autostartTimer?.Stop();
            _autostartTimer?.Dispose();
            _autostartTimer = null;
            await RunAutostartStrategyAsync();
        };
        _autostartTimer.Start();
    }

    private async Task RunAutostartStrategyAsync()
    {
        if (!RuntimeValidator.Validate(_runtimeLayout.RuntimeDirectory).IsComplete)
        {
            _logger.Info("Autostart skipped: runtime is missing.");
            _notifications.ShowInformation("Автозапуск пропущен: runtime не найден.");
            return;
        }

        if (GetSelectedStrategy() is null)
        {
            _logger.Info("Autostart skipped: strategy is not selected or missing.");
            _notifications.ShowInformation("Автозапуск пропущен: выбранная стратегия не найдена.");
            return;
        }

        await _zapretActionGate.WaitAsync();
        try
        {
            var result = await ExecuteZapretActionAsync(ZapretActionKind.Start);
            RefreshUi();
            _logger.Info($"Autostart strategy: {result.Outcome} — {result.Message}");
            if (!result.IsSuccess)
            {
                _notifications.ShowInformation("Не удалось автоматически включить zapret: " + result.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Autostart strategy failed.", ex);
            _notifications.ShowInformation("Не удалось автоматически включить zapret.");
        }
        finally
        {
            _zapretActionGate.Release();
        }
    }
}
