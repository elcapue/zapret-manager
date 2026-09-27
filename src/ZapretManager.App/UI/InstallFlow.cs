using System.Diagnostics;
using ZapretManager.App.Services;

namespace ZapretManager.App.UI;

/// <summary>
/// Запуск не из папки установки (обычно — только что скачанный файл). Ставит или обновляет
/// установленную копию и передаёт запуск ей. Работает без прав администратора и от имени
/// самого пользователя: так файлы и ярлыки попадают в его профиль, даже если UAC потом
/// подтвердят паролем другой учётной записи.
/// </summary>
internal static class InstallFlow
{
    public static void Run(InstallPaths paths, string executablePath)
    {
        var installedVersion = GetInstalledVersion(paths);
        var isNewerThanInstalled = installedVersion is null ||
                                   UpdateCheckService.IsNewer(ManagerUpdateService.CurrentVersion, installedVersion);
        if (isNewerThanInstalled)
        {
            var installer = new AppInstaller(paths);
            using var form = new InstallForm(
                paths,
                () => installer.Install(executablePath, ManagerUpdateService.CurrentVersion),
                installedVersion);
            if (form.ShowDialog() != DialogResult.OK)
            {
                return;
            }
        }

        using var installedInstance = new SingleInstanceService(paths.InstallDirectory);
        if (!installedInstance.TryAcquirePrimary(notifyPrimary: false))
        {
            // Установленный менеджер уже работает: показываем его окно, exe подменён для следующего запуска.
            installedInstance.TryAcquirePrimary();
            if (isNewerThanInstalled && installedVersion is not null)
            {
                ThemedMessageBox.Show(
                    $"Zapret Manager обновлён до версии {ManagerUpdateService.CurrentVersion}. " +
                    "Новая версия заработает после перезапуска: «Выход» в меню трея и запуск заново.",
                    "Zapret Manager");
            }

            return;
        }

        installedInstance.ReleasePrimary();
        var startInfo = new ProcessStartInfo(paths.ExecutablePath)
        {
            UseShellExecute = true,
            WorkingDirectory = paths.InstallDirectory
        };
        if (installedVersion is null)
        {
            startInfo.ArgumentList.Add(ApplicationLaunchOptions.FirstRunArgument);
        }

        Process.Start(startInfo)?.Dispose();
    }

    private static string? GetInstalledVersion(InstallPaths paths)
    {
        if (!File.Exists(paths.ExecutablePath))
        {
            return null;
        }

        var info = FileVersionInfo.GetVersionInfo(paths.ExecutablePath);
        return $"{info.FileMajorPart}.{info.FileMinorPart}.{info.FileBuildPart}";
    }
}
