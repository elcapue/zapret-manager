using System.Diagnostics;
using Microsoft.Win32;

namespace ZapretManager.App.Services;

/// <summary>
/// Установка для текущего пользователя: exe в папку установки, ярлыки в «Пуске» и на рабочем столе,
/// запись в «Параметры → Приложения» для штатного удаления. Прав администратора не требует.
/// </summary>
public sealed class AppInstaller
{
    internal const string UninstallArgument = "--uninstall";
    private const string DisplayName = "Zapret Manager";

    private readonly InstallPaths _paths;

    public AppInstaller(InstallPaths paths)
    {
        _paths = paths;
    }

    public void Install(string sourceExecutable, string version)
    {
        Directory.CreateDirectory(_paths.InstallDirectory);
        if (!PathComparer.AreEqual(sourceExecutable, _paths.ExecutablePath))
        {
            ExecutableReplacer.Replace(sourceExecutable, _paths.ExecutablePath);
        }

        ShellShortcut.Create(_paths.StartMenuShortcut, _paths.ExecutablePath, DisplayName);
        ShellShortcut.Create(_paths.DesktopShortcut, _paths.ExecutablePath, DisplayName);
        Register(version);
    }

    /// <summary>Обновляет запись в «Приложениях» — версия меняется после самообновления.</summary>
    public void Register(string version)
    {
        using var key = Registry.CurrentUser.CreateSubKey(_paths.UninstallRegistryKey, writable: true);
        key.SetValue("DisplayName", DisplayName);
        key.SetValue("DisplayVersion", version);
        key.SetValue("Publisher", "elcapue");
        key.SetValue("DisplayIcon", _paths.ExecutablePath);
        key.SetValue("InstallLocation", _paths.InstallDirectory);
        key.SetValue("UninstallString", $"\"{_paths.ExecutablePath}\" {UninstallArgument}");
        key.SetValue("URLInfoAbout", InstallPaths.RepositoryUrl);
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }


    /// <summary>Убирает ярлыки и запись в «Приложениях». Папку удаляет <see cref="ScheduleDirectoryDeletion"/>.</summary>
    public void RemoveIntegration()
    {
        DeleteFileIfExists(_paths.StartMenuShortcut);
        DeleteFileIfExists(_paths.DesktopShortcut);
        Registry.CurrentUser.DeleteSubKeyTree(_paths.UninstallRegistryKey, throwOnMissingSubKey: false);
    }

    /// <summary>
    /// Удаляет папку установки после выхода текущего процесса: работающий exe нельзя удалить из него самого.
    /// Удаляется только папка установки и только когда процесс запущен из неё.
    /// </summary>
    public void ScheduleDirectoryDeletion(int waitForProcessId)
    {
        var directory = Path.GetFullPath(_paths.InstallDirectory);
        // До 20 секунд ждём выхода процесса (пока он жив, ping работает секундной паузой), затем удаляем папку.
        var script =
            $"/d /c (for /l %i in (1,1,20) do @(tasklist /fi \"PID eq {waitForProcessId}\" 2>nul | find \"{waitForProcessId}\" >nul && ping -n 2 127.0.0.1 >nul)) " +
            $"& rmdir /s /q \"{directory}\"";
        Process.Start(new ProcessStartInfo("cmd.exe", script)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetTempPath()
        })?.Dispose();
    }

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
