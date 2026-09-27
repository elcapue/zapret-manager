namespace ZapretManager.App.Services;

/// <param name="IsAutostart">Запуск из Планировщика при входе в Windows: без окна, сразу включить стратегию.</param>
/// <param name="SkipInstall">
/// Служебный <c>--no-install</c> для разработки и проверок: работать прямо из текущей папки, не предлагая установку.
/// </param>
/// <param name="IsUninstall">Запуск из «Параметры → Приложения» для удаления программы.</param>
/// <param name="IsFirstRun">Первый запуск сразу после установки: runtime скачивается без лишнего клика.</param>
public sealed record ApplicationLaunchOptions(bool IsAutostart, bool SkipInstall, bool IsUninstall, bool IsFirstRun)
{
    internal const string AutostartArgument = "--autostart";
    internal const string SkipInstallArgument = "--no-install";
    internal const string FirstRunArgument = "--first-run";

    public static ApplicationLaunchOptions Parse(IEnumerable<string> arguments)
    {
        var set = new HashSet<string>(arguments, StringComparer.OrdinalIgnoreCase);
        return new ApplicationLaunchOptions(
            set.Contains(AutostartArgument),
            set.Contains(SkipInstallArgument),
            set.Contains(AppInstaller.UninstallArgument),
            set.Contains(FirstRunArgument));
    }
}
