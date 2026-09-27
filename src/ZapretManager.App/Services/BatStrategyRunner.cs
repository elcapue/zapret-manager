using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using ZapretManager.App.Core;
using ZapretManager.App.Infrastructure;

namespace ZapretManager.App.Services;

public sealed class BatStrategyRunner : IZapretRunner
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StartupPollInterval = TimeSpan.FromMilliseconds(100);
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];
    private static readonly Regex BackgroundStartPattern = new(
        "start\\s+(\"zapret:[^\"]*\")\\s+/min\\s+(\"%BIN%winws\\.exe\")",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    internal const string LaunchScriptFileName = "zapret-manager-run.cmd";

    private readonly ICommandRunner _commandRunner;
    private readonly AppConfig _config;
    private readonly ProcessSupervisor _processSupervisor;
    private readonly TimeSpan _startupTimeout;
    private readonly TimeSpan _startupPollInterval;

    public BatStrategyRunner(
        ICommandRunner commandRunner,
        AppConfig config,
        IWinwsProcessInspector? processInspector = null,
        TimeSpan? startupTimeout = null,
        TimeSpan? startupPollInterval = null)
    {
        _commandRunner = commandRunner;
        _config = config;
        _processSupervisor = new ProcessSupervisor(processInspector ?? new WinwsProcessInspector(), config);
        _startupTimeout = startupTimeout ?? StartupTimeout;
        _startupPollInterval = startupPollInterval ?? StartupPollInterval;
    }

    public ZapretRunResult Start(StrategyInfo? strategy)
    {
        if (strategy is null)
        {
            return new ZapretRunResult(ZapretRunStatus.NoStrategySelected, "Стратегия не выбрана.");
        }

        if (_processSupervisor.ReconcileManagedProcess() is not null)
        {
            return new ZapretRunResult(
                ZapretRunStatus.AlreadyRunningManaged,
                "zapret уже запущен менеджером.");
        }

        if (_processSupervisor.FindExternalProcesses().Count > 0)
        {
            return new ZapretRunResult(
                ZapretRunStatus.AlreadyRunningNotOwned,
                "Обнаружен внешний winws.exe. Закройте его через исходный launcher или вручную, затем повторите запуск. Менеджер не останавливает чужие процессы автоматически.");
        }

        var runtimeDirectory = Path.GetDirectoryName(strategy.FullPath);
        if (string.IsNullOrWhiteSpace(runtimeDirectory))
        {
            return new ZapretRunResult(ZapretRunStatus.CommandFailed, "Не удалось определить папку runtime для выбранной стратегии.");
        }

        var winwsPath = Path.Combine(runtimeDirectory, "bin", "winws.exe");
        if (!File.Exists(winwsPath))
        {
            return new ZapretRunResult(ZapretRunStatus.CommandFailed, "Не найден runtime\\bin\\winws.exe.");
        }

        WriteLaunchScript(runtimeDirectory, strategy.FullPath);

        var baseline = _processSupervisor.CaptureLaunchBaseline();
        var result = _commandRunner.StartDetached("cmd.exe", "/d /c " + LaunchScriptFileName, runtimeDirectory);
        if (result.ExitCode != 0)
        {
            return new ZapretRunResult(ZapretRunStatus.CommandFailed, result.StandardError);
        }

        if (!WaitForManagedProcess(baseline, winwsPath, strategy.FileName, out var processError))
        {
            return new ZapretRunResult(ZapretRunStatus.CommandFailed, processError);
        }

        return new ZapretRunResult(ZapretRunStatus.Started, $"Запущена стратегия: {strategy.DisplayName}");
    }

    public ZapretRunResult Stop()
    {
        return _processSupervisor.StopManagedProcess();
    }

    public ZapretRunResult Restart(StrategyInfo? strategy)
    {
        var stopResult = Stop();
        if (stopResult.Status is ZapretRunStatus.CommandFailed)
        {
            return stopResult;
        }

        var startResult = Start(strategy);
        return startResult.IsSuccess
            ? new ZapretRunResult(ZapretRunStatus.Restarted, startResult.Message)
            : startResult;
    }

    private bool WaitForManagedProcess(
        IReadOnlyList<WinwsProcessInfo> baseline,
        string winwsPath,
        string strategyFileName,
        out string errorMessage)
    {
        var stopwatch = Stopwatch.StartNew();
        do
        {
            if (_processSupervisor.TryRegisterNewProcess(baseline, winwsPath, strategyFileName, out errorMessage))
            {
                return true;
            }

            if (stopwatch.Elapsed < _startupTimeout && _startupPollInterval > TimeSpan.Zero)
            {
                Thread.Sleep(_startupPollInterval);
            }
        }
        while (stopwatch.Elapsed < _startupTimeout);

        errorMessage = "Не удалось подтвердить запуск winws.exe за отведённое время. " + errorMessage;
        return false;
    }

    /// <summary>
    /// Пишет копию стратегии, где winws.exe запускается в фоне (/b) вместо свёрнутого окна (/min).
    /// Файл лежит рядом со стратегией, потому что она опирается на %~dp0.
    /// Байты исходника сохраняются как есть (Latin1 — побайтовое преобразование): bat может быть
    /// в UTF-8 или OEM-кодировке, и перекодирование испортило бы кириллицу; UTF-8 BOM отбрасывается,
    /// иначе cmd не распознает первую строку.
    /// </summary>
    internal static void WriteLaunchScript(string runtimeDirectory, string strategyPath)
    {
        var bytes = File.ReadAllBytes(strategyPath);
        var offset = bytes.AsSpan().StartsWith(Utf8Bom) ? Utf8Bom.Length : 0;
        var content = Encoding.Latin1.GetString(bytes, offset, bytes.Length - offset);
        var background = BackgroundStartPattern.Replace(content, "start $1 /b $2");
        File.WriteAllText(
            Path.Combine(runtimeDirectory, LaunchScriptFileName),
            "@set NO_UPDATE_CHECK=1\r\n" + background,
            Encoding.Latin1);
    }
}
