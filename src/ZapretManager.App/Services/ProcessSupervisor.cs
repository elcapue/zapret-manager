using ZapretManager.App.Core;

namespace ZapretManager.App.Services;

public sealed class ProcessSupervisor
{
    private readonly IWinwsProcessInspector _processInspector;
    private readonly AppConfig _config;

    public ProcessSupervisor(IWinwsProcessInspector processInspector, AppConfig config)
    {
        _processInspector = processInspector;
        _config = config;
    }

    public WinwsProcessInfo? ReconcileManagedProcess()
    {
        var state = _config.ManagedProcess;
        if (state is null)
        {
            return null;
        }

        var process = _processInspector.FindRunning().FirstOrDefault(candidate =>
            candidate.ProcessId == state.ProcessId &&
            candidate.StartedAtUtc == state.StartedAtUtc &&
            (string.IsNullOrWhiteSpace(candidate.ExecutablePath) ||
             PathComparer.AreEqual(candidate.ExecutablePath, state.ExecutablePath)));
        if (process is not null)
        {
            return process;
        }

        ClearIfCurrent(state);
        return null;
    }

    public IReadOnlyList<WinwsProcessInfo> FindExternalProcesses()
    {
        var managedProcess = ReconcileManagedProcess();
        return _processInspector.FindRunning()
            .Where(candidate => managedProcess is null || candidate.ProcessId != managedProcess.ProcessId)
            .ToArray();
    }

    public IReadOnlyList<WinwsProcessInfo> CaptureLaunchBaseline()
    {
        return _processInspector.FindRunning();
    }

    public bool TryRegisterNewProcess(
        IReadOnlyList<WinwsProcessInfo> baseline,
        string executablePath,
        string strategyFileName,
        out string errorMessage)
    {
        var candidates = _processInspector.FindRunning()
            .Where(candidate => PathComparer.AreEqual(candidate.ExecutablePath, executablePath))
            .Where(candidate => !baseline.Any(existing =>
                existing.ProcessId == candidate.ProcessId &&
                existing.StartedAtUtc == candidate.StartedAtUtc))
            .ToArray();

        if (candidates.Length == 0)
        {
            errorMessage = "winws.exe не появился после запуска стратегии.";
            return false;
        }

        if (candidates.Length > 1)
        {
            errorMessage = "После запуска обнаружено несколько новых winws.exe; невозможно безопасно определить процесс менеджера.";
            return false;
        }

        var process = candidates[0];
        lock (_config)
        {
            _config.ManagedProcess = new ManagedProcessState
            {
                ProcessId = process.ProcessId,
                StartedAtUtc = process.StartedAtUtc,
                ExecutablePath = process.ExecutablePath,
                StrategyFileName = strategyFileName
            };
        }

        errorMessage = string.Empty;
        return true;
    }

    public ZapretRunResult StopManagedProcess()
    {
        var state = _config.ManagedProcess;
        var process = ReconcileManagedProcess();
        if (process is null || state is null)
        {
            return new ZapretRunResult(
                ZapretRunStatus.NotStartedByManager,
                "Запущенный процесс не найден");
        }

        if (!_processInspector.TryStop(process, out var errorMessage))
        {
            return new ZapretRunResult(ZapretRunStatus.CommandFailed, "Не удалось остановить управляемый winws.exe. " + errorMessage);
        }

        ClearIfCurrent(state);
        return new ZapretRunResult(ZapretRunStatus.Stopped, "zapret остановлен.");
    }

    /// <summary>
    /// Статус опрашивается из UI-потока, а запуск/остановка идут в фоне. Сбрасываем состояние,
    /// только если оно не успело смениться, иначе UI мог бы «потерять» только что запущенный winws.
    /// </summary>
    private void ClearIfCurrent(ManagedProcessState state)
    {
        lock (_config)
        {
            if (ReferenceEquals(_config.ManagedProcess, state))
            {
                _config.ManagedProcess = null;
            }
        }
    }
}
