using ZapretManager.App.Core;
using ZapretManager.App.Services;

namespace ZapretManager.App.UI;

/// <summary>
/// Слежение за состоянием zapret, пока менеджер в трее: мгновенная реакция на завершение своего
/// и внешнего winws.exe и опрос — раз в минуту, чтобы заметить внешний zapret, запущенный в обход
/// менеджера, и раз в 2 секунды, пока внешний zapret работает. Иконка и подсказка трея всегда актуальны.
/// </summary>
public sealed partial class TrayApplicationContext
{
    internal const int StatusRefreshIntervalMilliseconds = 60_000;

    // Внешний zapret — временное состояние, и не обо всём его завершении Windows сообщает событием:
    // служба zapret ещё секунду-другую числится запущенной после выхода winws.exe, а к чужому
    // процессу может не быть доступа. Поэтому, пока он работает, состояние перепроверяется часто.
    internal const int ExternalStatusRefreshIntervalMilliseconds = 2_000;
    private const int MaximumTrayTextLength = 127;

    private readonly System.Windows.Forms.Timer _statusTimer = new() { Interval = StatusRefreshIntervalMilliseconds };
    private ManagedProcessState? _watchedProcess;
    private CancellationTokenSource? _processWatchCancellation;
    private string _watchedExternalProcesses = string.Empty;
    private CancellationTokenSource? _externalWatchCancellation;

    private void StartStatusMonitoring()
    {
        _statusTimer.Tick += (_, _) => OnStatusTimerTick();
        _statusTimer.Start();
        UpdateTrayPresentation();
        WatchManagedProcess();
    }

    private void StopStatusMonitoring()
    {
        _statusTimer.Stop();
        CancelProcessWatch();
        CancelExternalWatch();
    }

    private void OnStatusTimerTick()
    {
        // Во время действия с zapret состояние меняется намеренно; действие само обновит UI в конце.
        if (_exitInProgress || _zapretActionGate.CurrentCount == 0)
        {
            return;
        }

        // Детектирование ходит по файлам, процессам и SCM — исключение здесь не должно ронять приложение.
        try
        {
            var previous = _state;
            UpdateState();
            if (previous != _state)
            {
                _mainForm.RefreshState();
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Status refresh failed.", ex);
        }
    }

    /// <summary>Определяет состояние, обновляет трей и слежение за процессом. Окно не трогает.</summary>
    private void UpdateState()
    {
        _state = DetectState();
        _statusTimer.Interval = _state == ZapretState.External
            ? ExternalStatusRefreshIntervalMilliseconds
            : StatusRefreshIntervalMilliseconds;
        UpdateTrayPresentation();
        WatchManagedProcess();
        WatchExternalProcesses();
    }

    private void UpdateTrayPresentation()
    {
        _notifyIcon.Icon = _trayIcons.Get(_state);
        var text = "Zapret Manager — " + _state.ToDisplayText().ToLowerInvariant();
        var strategyFileName = _config.ManagedProcess?.StrategyFileName;
        if (_state == ZapretState.Running && !string.IsNullOrWhiteSpace(strategyFileName))
        {
            text += Environment.NewLine + Path.GetFileNameWithoutExtension(strategyFileName);
        }

        _notifyIcon.Text = text.Length <= MaximumTrayTextLength
            ? text
            : text[..(MaximumTrayTextLength - 1)] + "…";
    }

    private void WatchManagedProcess()
    {
        var state = _config.ManagedProcess;
        if (ReferenceEquals(state, _watchedProcess))
        {
            return;
        }

        CancelProcessWatch();
        _watchedProcess = state;
        if (state is null)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        _processWatchCancellation = cancellation;
        _ = WatchManagedProcessAsync(state, cancellation.Token);
    }

    private void CancelProcessWatch()
    {
        _processWatchCancellation?.Cancel();
        _processWatchCancellation?.Dispose();
        _processWatchCancellation = null;
        _watchedProcess = null;
    }

    private async Task WatchManagedProcessAsync(ManagedProcessState state, CancellationToken cancellationToken)
    {
        bool exited;
        try
        {
            exited = await ManagedProcessWatcher.WaitForExitAsync(state, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        // Продолжение выполняется в UI-потоке. Если за это время слежение отменили (менеджер сам
        // остановил или сменил процесс и уже обновил UI), это не падение.
        if (!exited || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        OnManagedProcessExited(state);
    }

    /// <summary>Пока работает внешний zapret, ждём завершения его winws.exe, чтобы сразу показать «Выключено».</summary>
    private void WatchExternalProcesses()
    {
        var processes = _state == ZapretState.External
            ? _processSupervisor.FindExternalProcesses()
            : [];
        var key = string.Join(',', processes.Select(process => process.ProcessId));
        if (key == _watchedExternalProcesses)
        {
            return;
        }

        CancelExternalWatch();
        _watchedExternalProcesses = key;
        if (processes.Count == 0)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        _externalWatchCancellation = cancellation;
        _ = WatchExternalProcessesAsync(processes, cancellation.Token);
    }

    private void CancelExternalWatch()
    {
        _externalWatchCancellation?.Cancel();
        _externalWatchCancellation?.Dispose();
        _externalWatchCancellation = null;
        _watchedExternalProcesses = string.Empty;
    }

    private async Task WatchExternalProcessesAsync(IReadOnlyList<WinwsProcessInfo> processes, CancellationToken cancellationToken)
    {
        bool exited;
        try
        {
            exited = await ManagedProcessWatcher.WaitForAnyExitAsync(processes, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!exited)
        {
            _logger.Info("External winws.exe cannot be watched for exit; relying on the status refresh.");
            return;
        }

        if (cancellationToken.IsCancellationRequested || _exitInProgress)
        {
            return;
        }

        // Продолжение в UI-потоке. Во время своего действия состояние обновит само действие.
        _watchedExternalProcesses = string.Empty;
        if (_zapretActionGate.CurrentCount > 0)
        {
            RefreshUi();
        }
    }

    private void OnManagedProcessExited(ManagedProcessState state)
    {
        // Штатные остановки идут под gate; их результат показывает само действие.
        if (_exitInProgress || _zapretActionGate.CurrentCount == 0)
        {
            return;
        }

        _logger.Info($"Managed winws.exe (PID {state.ProcessId}, {state.StrategyFileName}) exited unexpectedly.");
        RefreshUi();
        _configService.Save(_config);
        _notifications.ShowInformation(
            "zapret неожиданно остановился. Нажмите, чтобы включить снова.",
            () => RunZapretAction(ZapretActionKind.Start));
    }
}
