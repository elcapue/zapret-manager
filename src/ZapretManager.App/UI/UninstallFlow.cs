using ZapretManager.App.Infrastructure;
using ZapretManager.App.Services;

namespace ZapretManager.App.UI;

/// <summary>
/// Удаление из «Параметры → Приложения». Выполняется с правами администратора: нужно остановить
/// свой winws.exe и снять задание автозапуска, созданное с наивысшими правами.
/// </summary>
internal static class UninstallFlow
{
    private static readonly TimeSpan RunningInstanceExitTimeout = TimeSpan.FromSeconds(30);

    public static void Run(InstallPaths paths, string executablePath, IAppLogger logger)
    {
        var confirmation = ThemedMessageBox.ShowAction(
            "Удалить Zapret Manager?\n\nБудут удалены программа, скачанный zapret (Flowseal), настройки и ярлыки. " +
            "Если zapret включён, он будет остановлен.",
            "Zapret Manager",
            "Удалить",
            "Отмена");
        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        using var singleInstance = new SingleInstanceService(AppContext.BaseDirectory);
        if (!singleInstance.TryAcquirePrimary(notifyPrimary: false))
        {
            // Работающий менеджер завершается штатно и сам останавливает свой zapret.
            singleInstance.RequestPrimaryExit();
            if (!singleInstance.TryAcquirePrimary(RunningInstanceExitTimeout, notifyPrimary: false))
            {
                ShowError("Не удалось закрыть работающий Zapret Manager. Выйдите из него через меню трея и повторите удаление.");
                return;
            }
        }

        // Если менеджер когда-то завершился аварийно, его winws.exe мог остаться — останавливаем только его.
        var config = new ConfigService(Path.Combine(AppContext.BaseDirectory, "config.json")).LoadOrCreate();
        var stop = new ProcessSupervisor(new WinwsProcessInspector(), config).StopManagedProcess();
        if (stop.Status == ZapretRunStatus.CommandFailed)
        {
            ShowError("Не удалось остановить zapret, поэтому удаление отменено.\n\n" + stop.Message);
            return;
        }

        var autostart = new AutostartService(new CommandRunner(), executablePath).SetEnabled(false);
        if (!autostart.IsSuccess)
        {
            logger.Info("Uninstall: " + autostart.Message);
        }

        var installer = new AppInstaller(paths);
        installer.RemoveIntegration();
        if (paths.IsInstalledCopy(executablePath))
        {
            installer.ScheduleDirectoryDeletion(Environment.ProcessId);
        }
    }

    private static void ShowError(string message)
    {
        ThemedMessageBox.Show(message, "Zapret Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
