using ZapretManager.App.Core;
using ZapretManager.App.Infrastructure;

namespace ZapretManager.App.Services;

public sealed class ExistingZapretStopService
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(15);

    private readonly ICommandRunner _commandRunner;
    private readonly IWinwsProcessInspector _processInspector;
    private readonly string _runtimeWinwsPath;

    public ExistingZapretStopService(
        ICommandRunner commandRunner,
        IWinwsProcessInspector processInspector,
        string runtimeDirectory)
    {
        _commandRunner = commandRunner;
        _processInspector = processInspector;
        _runtimeWinwsPath = Path.Combine(runtimeDirectory, "bin", "winws.exe");
    }

    public IReadOnlyList<WinwsProcessInfo> GetCurrentRuntimeProcesses()
    {
        return _processInspector.FindRunning()
            .Where(process => PathComparer.AreEqual(process.ExecutablePath, _runtimeWinwsPath))
            .ToArray();
    }

    public ExistingZapretStopResult StopExisting(ZapretStatus status)
    {
        var stopped = new List<string>();

        if (status.ZapretServiceRunning)
        {
            var serviceResult = _commandRunner.Run("sc.exe", "stop zapret", CommandTimeout);
            if (serviceResult.ExitCode != 0)
            {
                return Failed("Не удалось остановить service zapret", serviceResult);
            }

            stopped.Add("service zapret");
        }

        foreach (var process in GetCurrentRuntimeProcesses())
        {
            if (!_processInspector.TryStop(process, out var errorMessage))
            {
                return new ExistingZapretStopResult(
                    ExistingZapretStopStatus.CommandFailed,
                    $"Не удалось остановить winws.exe (PID {process.ProcessId}) из текущего runtime. {errorMessage}");
            }

            stopped.Add($"winws.exe (PID {process.ProcessId})");
        }

        if (stopped.Count == 0)
        {
            return new ExistingZapretStopResult(
                ExistingZapretStopStatus.NothingToStop,
                "В текущем runtime нет активного zapret. Внешние winws.exe из других папок менеджер не останавливает.");
        }

        return new ExistingZapretStopResult(ExistingZapretStopStatus.Stopped, "Остановлено: " + string.Join(", ", stopped) + ".");
    }

    private static ExistingZapretStopResult Failed(string prefix, CommandResult result)
    {
        var details = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput
            : result.StandardError;
        return new ExistingZapretStopResult(ExistingZapretStopStatus.CommandFailed, prefix + ". " + details);
    }
}
