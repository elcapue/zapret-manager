using System.Diagnostics;
using ZapretManager.App.Services;

namespace ZapretManager.App.UI;

public sealed partial class TrayApplicationContext
{
    private enum ManagerUpdateOutcome
    {
        /// <summary>Проверить не удалось (нет сети, у репозитория ещё нет релизов) — это не повод беспокоить пользователя.</summary>
        Unknown,
        UpToDate,
        /// <summary>Об обновлении сообщено или оно установлено — проверку Flowseal откладываем, чтобы не сыпать окнами.</summary>
        Offered
    }

    private async Task<ManagerUpdateOutcome> CheckManagerUpdateAsync(HttpClient httpClient, bool isAutomaticStartupCheck)
    {
        var service = new ManagerUpdateService(httpClient);
        ManagerUpdateCheck check;
        try
        {
            check = await service.CheckAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.Error("Manager update check failed.", ex);
            return ManagerUpdateOutcome.Unknown;
        }

        if (!check.IsAvailable)
        {
            return ManagerUpdateOutcome.UpToDate;
        }

        _logger.Info($"Manager update available: {check.CurrentVersion} -> {check.LatestVersion}.");
        if (isAutomaticStartupCheck)
        {
            _notifications.ShowInformation(
                $"Доступна новая версия Zapret Manager {check.LatestVersion}. Нажмите, чтобы обновить.",
                () => _ = CheckUpdatesAsync());
            return ManagerUpdateOutcome.Offered;
        }

        var confirmation = ThemedMessageBox.ShowAction(
            "Доступна новая версия Zapret Manager.\n\n" +
            $"Текущая версия: {check.CurrentVersion}\nНовая версия: {check.LatestVersion}\n\n" +
            "zapret продолжит работать во время обновления.",
            "Zapret Manager",
            "Обновить",
            "Позже",
            MessageBoxIcon.Question);
        if (confirmation == DialogResult.Yes)
        {
            await InstallManagerUpdateAsync(service, check);
        }

        return ManagerUpdateOutcome.Offered;
    }

    private async Task InstallManagerUpdateAsync(ManagerUpdateService service, ManagerUpdateCheck check)
    {
        // Скачивание идёт без gate: оно длительное и не должно блокировать действия с zapret и выход.
        // Gate нужен только на короткую подмену exe.
        string downloaded;
        try
        {
            downloaded = await service.DownloadAsync(check.Asset!, _runtimeLayout.TempDirectory, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.Error("Manager update download failed.", ex);
            ThemedMessageBox.Show(
                $"Не удалось обновить Zapret Manager.\n\n{ex.Message}",
                "Zapret Manager",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (!await _zapretActionGate.WaitAsync(0))
        {
            File.Delete(downloaded);
            _notifications.ShowInformation("Сейчас выполняется действие с zapret. Обновите менеджер чуть позже.");
            return;
        }

        var releaseGate = true;
        try
        {
            var executablePath = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, InstallPaths.ExecutableName);
            string? previous;
            try
            {
                previous = ExecutableReplacer.Replace(downloaded, executablePath);
            }
            finally
            {
                File.Delete(downloaded);
            }

            if (!TryStartUpdatedInstance(executablePath))
            {
                ExecutableReplacer.RollBack(executablePath, previous);
                ThemedMessageBox.Show(
                    "Не удалось запустить новую версию Zapret Manager, оставлена текущая.",
                    "Zapret Manager",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            _logger.Info($"Manager updated to {check.LatestVersion}, handing over to the new instance.");
            releaseGate = false;
            ExitForManagerUpdate();
        }
        catch (Exception ex)
        {
            _logger.Error("Manager update install failed.", ex);
            ThemedMessageBox.Show(
                $"Не удалось обновить Zapret Manager.\n\n{ex.Message}",
                "Zapret Manager",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        finally
        {
            if (releaseGate)
            {
                _zapretActionGate.Release();
            }
        }
    }

    /// <summary>
    /// Новый процесс наследует права администратора и ждёт, пока этот освободит single-instance mutex
    /// (тот же механизм, что при повышении прав на старте).
    /// </summary>
    private bool TryStartUpdatedInstance(string executablePath)
    {
        try
        {
            var startInfo = new ProcessStartInfo(executablePath)
            {
                UseShellExecute = false,
                WorkingDirectory = AppContext.BaseDirectory
            };
            startInfo.ArgumentList.Add(SingleInstanceService.ElevationHandoffArgument);
            using var process = Process.Start(startInfo);
            return process is not null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            _logger.Error("Updated instance failed to start.", ex);
            return false;
        }
    }

    /// <summary>Выход без остановки zapret: новая версия подхватит запущенный winws.exe.</summary>
    private void ExitForManagerUpdate()
    {
        _exitInProgress = true;
        _strategyAutoSelectionCancellation?.Cancel();
        CompleteExit();
    }
}
