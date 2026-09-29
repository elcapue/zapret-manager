using ZapretManager.App.Services;
using ZapretManager.App.UI;

namespace ZapretManager.App;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var launchOptions = ApplicationLaunchOptions.Parse(args);

        // Служебный вызов install-local.ps1: имя pipe знает только SingleInstanceService.
        if (launchOptions.RequestPrimaryExitDirectory is not null)
        {
            new SingleInstanceService(launchOptions.RequestPrimaryExitDirectory).RequestPrimaryExit();
            return;
        }

        var installPaths = InstallPaths.ForCurrentUser();
        var executablePath = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, InstallPaths.ExecutableName);

        // Скачанный файл ничего не создаёт рядом с собой: только ставит программу и передаёт ей запуск.
        if (!launchOptions.SkipInstall && !launchOptions.IsUninstall && !installPaths.IsInstalledCopy(executablePath))
        {
            InstallFlow.Run(installPaths, executablePath);
            return;
        }

        var runtimeLayout = RuntimeLayout.ForDirectory(AppContext.BaseDirectory);
        var logger = new FileLogger(runtimeLayout);

        // Страховка на случай исключений вне обработанных catch: хотя бы оставляем след в логе.
        Application.ThreadException += (_, eventArgs) =>
            logger.Error("UI thread exception.", eventArgs.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception exception)
            {
                logger.Error("Unhandled exception.", exception);
            }
        };

        if (!ElevationService.IsRunningAsAdministrator())
        {
            if (launchOptions.IsUninstall)
            {
                ElevationService.TryRestartElevated(logger, args);
                return;
            }

            using var probe = new SingleInstanceService(AppContext.BaseDirectory);
            if (probe.TryAcquirePrimary())
            {
                // Elevated-процесс ждёт освобождения mutex, поэтому не примет
                // medium-предшественника за уже работающий основной экземпляр.
                var elevatedArguments = args.Append(SingleInstanceService.ElevationHandoffArgument);
                if (ElevationService.TryRestartElevated(logger, elevatedArguments))
                {
                    probe.ReleasePrimary();
                    return;
                }

                // Пользователь отклонил UAC — без прав администратора менеджер бесполезен.
                ThemedMessageBox.Show(
                    "Zapret Manager требует права администратора для управления zapret.",
                    "Zapret Manager",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            // Экземпляр уже запущен — попросили его показать окно, UAC не нужен.
            return;
        }

        if (launchOptions.IsUninstall)
        {
            UninstallFlow.Run(installPaths, executablePath, logger);
            return;
        }

        using var singleInstance = new SingleInstanceService(AppContext.BaseDirectory);
        var isElevationHandoff = args.Any(argument =>
            string.Equals(argument, SingleInstanceService.ElevationHandoffArgument, StringComparison.OrdinalIgnoreCase));
        if (!singleInstance.TryAcquirePrimary(isElevationHandoff ? TimeSpan.FromSeconds(10) : TimeSpan.Zero))
        {
            return;
        }

        // Прошлая версия, оставшаяся после самообновления, больше не запущена.
        ExecutableReplacer.DeleteLeftover(executablePath);
        if (installPaths.IsInstalledCopy(executablePath))
        {
            RefreshInstallRegistration(installPaths, logger);
        }

        var configService = new ConfigService(runtimeLayout.ConfigPath);
        var bootstrapResult = new AppBootstrapper(configService, runtimeLayout).Initialize();
        var bootstrapService = new RuntimeBootstrapService(runtimeLayout);
        if (bootstrapService.ShouldOfferBootstrap() && !launchOptions.IsAutostart)
        {
            using var bootstrapForm = new RuntimeBootstrapForm(
                bootstrapService,
                configService,
                bootstrapResult.Config,
                startDownloadImmediately: launchOptions.IsFirstRun);
            if (bootstrapForm.ShowDialog() != DialogResult.OK)
            {
                return;
            }
        }

        var commandRunner = new Infrastructure.CommandRunner();
        var processInspector = new WinwsProcessInspector();
        var services = new AppServices(
            configService,
            bootstrapResult.Config,
            runtimeLayout,
            logger,
            commandRunner,
            processInspector,
            new ProcessSupervisor(processInspector, bootstrapResult.Config),
            new AutostartService(commandRunner, executablePath),
            new ZapretActionExecutor(configService, runtimeLayout, bootstrapResult.Config, commandRunner, processInspector));

        if (bootstrapResult.Config.StartWithWindows && !launchOptions.IsAutostart)
        {
            var autostartResult = services.AutostartService.SetEnabled(enabled: true);
            if (!autostartResult.IsSuccess)
            {
                logger.Info(autostartResult.Message);
            }
        }

        Application.Run(new TrayApplicationContext(singleInstance, services, launchOptions.IsAutostart));
    }

    /// <summary>Версия в «Приложениях» меняется после самообновления; заодно запись восстанавливается, если её удалили.</summary>
    private static void RefreshInstallRegistration(InstallPaths installPaths, IAppLogger logger)
    {
        try
        {
            new AppInstaller(installPaths).Register(ManagerUpdateService.CurrentVersion);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            logger.Error("Install registration refresh failed.", ex);
        }
    }
}
