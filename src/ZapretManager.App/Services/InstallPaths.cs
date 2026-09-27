namespace ZapretManager.App.Services;

/// <summary>
/// Где живёт установленный менеджер. Установка — для текущего пользователя, в его профиль:
/// сам exe и все данные (runtime, config.json, логи) лежат в одной папке, как раньше рядом с exe.
/// </summary>
public sealed record InstallPaths(
    string InstallDirectory,
    string StartMenuShortcut,
    string DesktopShortcut,
    string UninstallRegistryKey)
{
    public const string ExecutableName = "Zapret Manager.exe";
    public const string RepositoryUrl = "https://github.com/elcapue/zapret-manager";

    public string ExecutablePath => Path.Combine(InstallDirectory, ExecutableName);

    public static InstallPaths ForCurrentUser()
    {
        return new InstallPaths(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                "Zapret Manager"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Zapret Manager.lnk"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Zapret Manager.lnk"),
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ZapretManager");
    }

    public bool IsInstalledCopy(string executablePath)
    {
        return PathComparer.AreEqual(Path.GetDirectoryName(executablePath) ?? string.Empty, InstallDirectory);
    }
}
