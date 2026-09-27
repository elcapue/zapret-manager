using ZapretManager.App.Services;

namespace ZapretManager.App.UI;

public sealed partial class TrayApplicationContext
{
    private void RunStrategyTests()
    {
        if (_exitInProgress)
        {
            return;
        }

        if (_strategyAutoSelectionTask is { IsCompleted: false })
        {
            _notifications.ShowInformation("Автовыбор уже выполняется.");
            return;
        }

        _strategyAutoSelectionTask = RunStrategyTestsAsync();
    }

    private async Task RunStrategyTestsAsync()
    {
        // Gate берётся до диалогов: пока пользователь читает подтверждение,
        // Start/Stop из трея не должны менять состояние winws под автовыбором.
        if (!await _zapretActionGate.WaitAsync(0))
        {
            _notifications.ShowInformation("Другое действие с zapret уже выполняется.");
            return;
        }

        try
        {
            if (!ConfirmStrategyTests())
            {
                return;
            }

            ShowMainWindow();
            _mainForm.BeginStrategyAutoSelection(GetStrategies().Count);

            using var cancellation = new CancellationTokenSource();
            _strategyAutoSelectionCancellation = cancellation;
            var progress = new Progress<StrategyAutoSelectionProgress>(_mainForm.UpdateStrategyAutoSelectionProgress);
            var response = await ExecuteZapretActionAsync(
                ZapretActionKind.AutoSelect,
                progress,
                cancellation.Token);
            RefreshUi();

            if (response.Outcome == ZapretActionOutcome.Cancelled)
            {
                _logger.Info("Built-in strategy auto-selection cancelled by user.");
                _mainForm.FinishStrategyAutoSelectionWithError(response.Message);
                return;
            }

            var result = response.AutoSelection;
            if (result is null)
            {
                _logger.Info($"Built-in strategy auto-selection failed: {response.Message}");
                _mainForm.FinishStrategyAutoSelectionWithError(response.Message);
                ThemedMessageBox.Show(response.Message, "Zapret Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _logger.Info($"Built-in strategy auto-selection: {result.Status} — {result.Message}");
            foreach (var note in result.Notes)
            {
                _logger.Info("Auto-selection note: " + note);
            }

            _mainForm.ShowStrategyAutoSelectionResult(result);
            if (result.Status == StrategyAutoSelectionStatus.StopFailed)
            {
                // zapret не удалось остановить — это требует внимания, а не просто уведомления.
                ThemedMessageBox.Show(result.Message, "Zapret Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Коротко: итог и доля проверок. Подробности — в окне, по наведению на результат.
            _notifications.ShowInformation(BuildStrategyAutoSelectionSummary(result), ShowMainWindow);
        }
        catch (Exception ex)
        {
            _logger.Error("Built-in strategy auto-selection failed.", ex);
            _mainForm.FinishStrategyAutoSelectionWithError("Автовыбор стратегии завершился ошибкой: " + ex.Message);
            ThemedMessageBox.Show("Автовыбор стратегии завершился ошибкой.\n\n" + ex.Message, "Zapret Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _strategyAutoSelectionCancellation = null;
            _zapretActionGate.Release();
        }
    }

    private bool ConfirmStrategyTests()
    {
        if (DetectZapretStatus().ZapretServiceExists)
        {
            var removeConfirmation = ThemedMessageBox.Show(
                "Для автовыбора нужно удалить Windows service zapret. Удалить его сейчас?\n\nВнешние winws.exe и WinDivert-службы не изменятся.",
                "Zapret Manager",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (removeConfirmation != DialogResult.Yes)
            {
                _notifications.ShowInformation("Автовыбор отменён: Windows service zapret не удалён.");
                return false;
            }
        }

        var confirmation = ThemedMessageBox.Show(
            "Проверить все доступные стратегии и выбрать лучшую?\n\n" +
            "Сначала сайты проверяются без zapret, затем каждая стратегия, а лучшие перепроверяются ещё раз. " +
            "Это займёт несколько минут; на время проверки zapret будет перезапускаться.",
            "Zapret Manager",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        return confirmation == DialogResult.Yes;
    }

    private void CancelStrategyTests()
    {
        var cancellation = _strategyAutoSelectionCancellation;
        if (cancellation is null)
        {
            _mainForm.FinishStrategyAutoSelectionWithError("Автовыбор сейчас не выполняется.");
            return;
        }

        _mainForm.SetStrategyAutoSelectionStatus("Останавливаем автовыбор...");
        cancellation.Cancel();
    }

    internal static string BuildStrategyAutoSelectionSummary(StrategyAutoSelectionResult result)
    {
        var summary = "Автовыбор завершён. " + result.Message;
        if (!result.IsSuccess || result.TopStrategies.Count == 0)
        {
            return summary;
        }

        var best = result.TopStrategies[0];
        return summary + Environment.NewLine + $"Прошли {best.HttpOk} из {best.HttpOk + best.HttpError} проверок.";
    }
}
