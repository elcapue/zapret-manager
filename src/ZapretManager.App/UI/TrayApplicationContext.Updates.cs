using ZapretManager.App.Core;
using ZapretManager.App.Services;

namespace ZapretManager.App.UI;

public sealed partial class TrayApplicationContext
{
    private static readonly TimeSpan AutostartUpdateCheckDelay = TimeSpan.FromMinutes(1);

    private async void ScheduleAutomaticUpdateCheck(TimeSpan delay)
    {
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay);
        }

        if (_exitInProgress)
        {
            return;
        }

        await CheckUpdatesAsync(isAutomaticStartupCheck: true);
    }

    /// <summary>
    /// Ручная проверка ведёт диалогами до установки. Автоматическая ничего не спрашивает модально:
    /// о новой версии сообщает уведомление, клик по которому запускает ручной сценарий.
    /// Сначала проверяется сам менеджер, затем zapret (Flowseal).
    /// </summary>
    private async Task CheckUpdatesAsync(bool isAutomaticStartupCheck = false)
    {
        using var httpClient = new HttpClient();
        var managerOutcome = await CheckManagerUpdateAsync(httpClient, isAutomaticStartupCheck);
        if (managerOutcome == ManagerUpdateOutcome.Offered)
        {
            return;
        }

        GitHubReleaseInfo release;
        UpdateCheckResult result;
        try
        {
            release = await new GitHubReleaseClient(httpClient).GetLatestReleaseAsync(CancellationToken.None);
            result = UpdateCheckService.Compare(_config.LastKnownVersion, release);
        }
        catch (Exception ex)
        {
            _logger.Error("Update check failed.", ex);
            if (!isAutomaticStartupCheck)
            {
                ThemedMessageBox.Show($"Не удалось проверить обновление.\n\n{ex.Message}", "Zapret Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            return;
        }

        if (result.Availability == UpdateAvailability.UpToDate)
        {
            if (UpdateNotificationPolicy.ShouldShowMessage(result.Availability, isAutomaticStartupCheck))
            {
                // Один общий ответ на кнопку: менеджер и zapret проверяются вместе, пользователю важно лишь,
                // есть ли что ставить.
                _notifications.ShowInformation("Обновлений не найдено.");
            }

            return;
        }

        if (isAutomaticStartupCheck)
        {
            _logger.Info($"Update available: {result.CurrentVersion} -> {result.LatestVersion}.");
            _notifications.ShowInformation(
                $"Доступна версия zapret (Flowseal) {result.LatestVersion}. Нажмите, чтобы обновить.",
                () => _ = CheckUpdatesAsync());
            return;
        }

        await InstallUpdateAsync(httpClient, release, result);
    }

    private async Task InstallUpdateAsync(HttpClient httpClient, GitHubReleaseInfo release, UpdateCheckResult result)
    {
        try
        {
            if (result.ZipAsset is null)
            {
                ThemedMessageBox.Show(
                    BuildUpdateMessage(result) + "\n\nВ релизе нет ZIP-файла для установки.",
                    "Zapret Manager",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var confirmation = ThemedMessageBox.Show(
                BuildUpdateMessage(result) + "\n\nСкачать и установить это обновление?",
                "Zapret Manager",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirmation != DialogResult.Yes)
            {
                return;
            }

            var updateService = new UpdateService(httpClient, _runtimeLayout);
            var prepareResult = await updateService.PrepareUpdateAsync(release, CancellationToken.None);
            using var preparedUpdate = prepareResult.PreparedUpdate;
            if (preparedUpdate is null)
            {
                ThemedMessageBox.Show(prepareResult.Message, "Zapret Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!await _zapretActionGate.WaitAsync(0))
            {
                _notifications.ShowInformation("Другое действие с zapret уже выполняется.");
                return;
            }

            RuntimeUpdateOutcome outcome;
            try
            {
                var coordinator = new RuntimeUpdateCoordinator(
                    IsManagedProcessRunning,
                    () => CreateExistingZapretStopService().GetCurrentRuntimeProcesses().Count > 0,
                    action => ExecuteZapretActionAsync(action),
                    () =>
                    {
                        var applyResult = updateService.ApplyPreparedUpdate(preparedUpdate, _config);
                        _configService.Save(_config);
                        return applyResult;
                    });
                outcome = await coordinator.ApplyAsync();
                _logger.Info("Update: " + outcome.Message.Replace('\n', ' '));
                RefreshUi();
            }
            finally
            {
                _zapretActionGate.Release();
            }

            if (outcome.IsSuccess)
            {
                _notifications.ShowInformation(outcome.Message);
                return;
            }

            ThemedMessageBox.Show(outcome.Message, "Zapret Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            _logger.Error("Update install failed.", ex);
            ThemedMessageBox.Show($"Не удалось установить обновление.\n\n{ex.Message}", "Zapret Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static string BuildUpdateMessage(UpdateCheckResult result)
    {
        var status = result.Availability switch
        {
            UpdateAvailability.UpToDate => "Установлена последняя версия zapret (Flowseal).",
            UpdateAvailability.UpdateAvailable => "Доступна новая версия zapret (Flowseal).",
            UpdateAvailability.UnknownCurrentVersion => "Текущая версия zapret (Flowseal) неизвестна. Последняя версия сохранена как базовая.",
            _ => "Статус обновления zapret (Flowseal) неизвестен."
        };

        return result.Availability == UpdateAvailability.UpToDate
            ? status
            : $"{status}\nЭто пакет стратегий и winws.exe, а не сам Zapret Manager.\n\n" +
              $"Текущая версия: {result.CurrentVersion}\nНовая версия: {result.LatestVersion}";
    }
}
