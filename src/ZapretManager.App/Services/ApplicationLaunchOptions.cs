namespace ZapretManager.App.Services;

/// <param name="IsAutostart">Запуск из Планировщика при входе в Windows: без окна, сразу включить стратегию.</param>
/// <param name="SkipInstall">
/// Служебный <c>--no-install</c> для разработки и проверок: работать прямо из текущей папки, не предлагая установку.
/// </param>
/// <param name="IsUninstall">Запуск из «Параметры → Приложения» для удаления программы.</param>
/// <param name="IsFirstRun">Первый запуск сразу после установки: runtime скачивается без лишнего клика.</param>
/// <param name="RequestPrimaryExitDirectory">
/// Служебный <c>--request-primary-exit &lt;папка&gt;</c> для install-local.ps1: попросить экземпляр
/// из указанной папки завершиться штатно. Имя pipe вычисляется только в SingleInstanceService —
/// протокол не дублируется снаружи.
/// </param>
public sealed record ApplicationLaunchOptions(
    bool IsAutostart,
    bool SkipInstall,
    bool IsUninstall,
    bool IsFirstRun,
    string? RequestPrimaryExitDirectory = null)
{
    internal const string AutostartArgument = "--autostart";
    internal const string SkipInstallArgument = "--no-install";
    internal const string FirstRunArgument = "--first-run";
    internal const string RequestPrimaryExitArgument = "--request-primary-exit";

    public static ApplicationLaunchOptions Parse(IEnumerable<string> arguments)
    {
        var list = arguments.ToArray();
        var set = new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
        string? exitTargetDirectory = null;
        for (var index = 0; index < list.Length - 1; index++)
        {
            if (string.Equals(list[index], RequestPrimaryExitArgument, StringComparison.OrdinalIgnoreCase))
            {
                exitTargetDirectory = list[index + 1];
                break;
            }
        }

        return new ApplicationLaunchOptions(
            set.Contains(AutostartArgument),
            set.Contains(SkipInstallArgument),
            set.Contains(AppInstaller.UninstallArgument),
            set.Contains(FirstRunArgument),
            exitTargetDirectory);
    }
}
