using ZapretManager.App.Core;
using ZapretManager.App.Infrastructure;

namespace ZapretManager.App.Services;

public sealed class ZapretActionExecutor
{
    private readonly ConfigService _configService;
    private readonly RuntimeLayout _runtimeLayout;
    private readonly AppConfig _config;

    public ZapretActionExecutor(ConfigService configService, RuntimeLayout runtimeLayout, AppConfig config)
    {
        _configService = configService;
        _runtimeLayout = runtimeLayout;
        _config = config;
    }

    public async Task<ZapretActionResponse> ExecuteAsync(
        ZapretActionKind action,
        IProgress<StrategyAutoSelectionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return action switch
            {
                ZapretActionKind.Start => FromZapretResult(CreateRunner().Start(GetSelectedStrategy())),
                ZapretActionKind.Stop => FromZapretResult(CreateRunner().Stop()),
                ZapretActionKind.Restart => FromZapretResult(CreateRunner().Restart(GetSelectedStrategy())),
                ZapretActionKind.StopExisting => StopExisting(),
                ZapretActionKind.AutoSelect => await AutoSelectAsync(progress, cancellationToken).ConfigureAwait(false),
                _ => new ZapretActionResponse(ZapretActionOutcome.Failed, "Неизвестное действие с zapret.")
            };
        }
        catch (Exception ex)
        {
            return new ZapretActionResponse(
                ZapretActionOutcome.Failed,
                "Не удалось выполнить действие с zapret. " + ex.Message);
        }
        finally
        {
            _configService.Save(_config);
        }
    }

    private ZapretActionResponse StopExisting()
    {
        var status = new ZapretDetectionService().Detect();
        var result = new ExistingZapretStopService(
            new CommandRunner(),
            new WinwsProcessInspector(),
            _runtimeLayout.RuntimeDirectory).StopExisting(status);
        if (result.IsSuccess)
        {
            _config.ManagedProcess = null;
        }

        return new ZapretActionResponse(
            result.IsSuccess ? ZapretActionOutcome.Succeeded : ZapretActionOutcome.Failed,
            result.Message);
    }

    private async Task<ZapretActionResponse> AutoSelectAsync(
        IProgress<StrategyAutoSelectionProgress>? progress,
        CancellationToken cancellationToken)
    {
        var status = new ZapretDetectionService().Detect();
        var runner = CreateRunner();
        var supervisor = new ProcessSupervisor(new WinwsProcessInspector(), _config);
        var wasRunning = status.ZapretServiceRunning || supervisor.ReconcileManagedProcess() is not null;

        if (status.ZapretServiceExists)
        {
            var removal = new ZapretServiceRemovalService(new CommandRunner()).RemoveForStrategyTests(status);
            if (!removal.IsSuccess)
            {
                return new ZapretActionResponse(ZapretActionOutcome.Failed, removal.Message);
            }
        }

        // Чужой winws.exe искажает и проверку без zapret, и каждую стратегию.
        if (supervisor.FindExternalProcesses().Count > 0)
        {
            return new ZapretActionResponse(
                ZapretActionOutcome.Failed,
                "Обнаружен внешний winws.exe. Закройте его, иначе результаты проверки стратегий будут недостоверны.");
        }

        var strategies = StrategyService.DiscoverStrategies(_runtimeLayout.RuntimeDirectory, _config.SelectedStrategy);
        var selector = new StrategyAutoSelectionService(
            runner,
            new HttpStrategyProbe(),
            targets: StrategyTargetCatalog.Load(_runtimeLayout.RuntimeDirectory));

        StrategyAutoSelectionResult result;
        try
        {
            result = await selector.RunAsync(strategies, cancellationToken, progress).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            runner.Stop();
            var restore = wasRunning ? " " + RestoreRunningState(runner) : string.Empty;
            return new ZapretActionResponse(ZapretActionOutcome.Cancelled, "Автовыбор остановлен пользователем." + restore);
        }

        if (result.BestStrategy is not null)
        {
            StrategySelectionService.SelectStrategy(_configService, _config, result.BestStrategy);
            _configService.SaveLastSuccessfulStrategyScan(_config, result, DateTimeOffset.UtcNow);
        }

        if (wasRunning && result.Status != StrategyAutoSelectionStatus.StopFailed)
        {
            result = result with { Notes = [.. result.Notes, RestoreRunningState(runner)] };
        }

        return new ZapretActionResponse(
            result.IsSuccess ? ZapretActionOutcome.Succeeded : ZapretActionOutcome.Failed,
            result.Message,
            result);
    }

    /// <summary>zapret работал до автовыбора — запускаем выбранную (новую лучшую или прежнюю) стратегию.</summary>
    private string RestoreRunningState(BatStrategyRunner runner)
    {
        var start = runner.Start(GetSelectedStrategy());
        return start.IsSuccess
            ? "zapret снова включён."
            : "Не удалось снова включить zapret: " + start.Message;
    }

    private BatStrategyRunner CreateRunner()
    {
        return new BatStrategyRunner(new CommandRunner(), _config);
    }

    private StrategyInfo? GetSelectedStrategy()
    {
        return StrategyService
            .DiscoverStrategies(_runtimeLayout.RuntimeDirectory, _config.SelectedStrategy)
            .FirstOrDefault(strategy => strategy.IsSelected);
    }

    private static ZapretActionResponse FromZapretResult(ZapretRunResult result)
    {
        return new ZapretActionResponse(
            result.IsSuccess ? ZapretActionOutcome.Succeeded : ZapretActionOutcome.Failed,
            result.Message);
    }
}
