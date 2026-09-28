using ZapretManager.App.Core;
using ZapretManager.App.Infrastructure;

namespace ZapretManager.App.Services;

/// <summary>
/// Состав приложения, собранный один раз в Program: долгоживущие зависимости
/// и единственная копия конфига. Сервисы по месту (new в обработчиках) не создаём.
/// </summary>
public sealed class AppServices
{
    public AppServices(
        ConfigService configService,
        AppConfig config,
        RuntimeLayout runtimeLayout,
        IAppLogger logger,
        ICommandRunner commandRunner,
        IWinwsProcessInspector processInspector,
        ProcessSupervisor processSupervisor,
        AutostartService autostartService,
        ZapretActionExecutor actionExecutor)
    {
        ConfigService = configService;
        Config = config;
        RuntimeLayout = runtimeLayout;
        Logger = logger;
        CommandRunner = commandRunner;
        ProcessInspector = processInspector;
        ProcessSupervisor = processSupervisor;
        AutostartService = autostartService;
        ActionExecutor = actionExecutor;
    }

    public ConfigService ConfigService { get; }

    public AppConfig Config { get; }

    public RuntimeLayout RuntimeLayout { get; }

    public IAppLogger Logger { get; }

    public ICommandRunner CommandRunner { get; }

    public IWinwsProcessInspector ProcessInspector { get; }

    public ProcessSupervisor ProcessSupervisor { get; }

    public AutostartService AutostartService { get; }

    public ZapretActionExecutor ActionExecutor { get; }
}
