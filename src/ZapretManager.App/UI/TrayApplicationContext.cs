using System.ComponentModel;
using ZapretManager.App.Core;
using ZapretManager.App.Infrastructure;
using ZapretManager.App.Services;

namespace ZapretManager.App.UI;

public sealed partial class TrayApplicationContext : ApplicationContext
{
    private readonly ConfigService _configService;
    private readonly RuntimeLayout _runtimeLayout;
    private readonly AppConfig _config;
    private readonly FileLogger _logger;
    private readonly ProcessSupervisor _processSupervisor;
    private readonly TrayIconSet _trayIcons;
    private readonly NotifyIcon _notifyIcon;
    private readonly TrayNotificationService _notifications;
    private readonly TrayMenuActions _trayMenuActions;
    private readonly MainForm _mainForm;
    private readonly AutostartService _autostartService;
    private readonly SemaphoreSlim _zapretActionGate = new(1, 1);
    private CancellationTokenSource? _strategyAutoSelectionCancellation;
    private Task? _strategyAutoSelectionTask;
    private bool _exitInProgress;
    private bool _isExiting;
    private bool _runtimePromptOpen;
    private ZapretState _state;
    private readonly SingleInstanceService _singleInstance;
    private System.Windows.Forms.Timer? _autostartTimer;

    public TrayApplicationContext(SingleInstanceService singleInstance, bool startHidden = false)
    {
        _singleInstance = singleInstance;
        var configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
        _configService = new ConfigService(configPath);
        _runtimeLayout = RuntimeLayout.ForDirectory(AppContext.BaseDirectory);
        var bootstrapper = new AppBootstrapper(_configService, _runtimeLayout);
        var bootstrapResult = bootstrapper.Initialize();
        _config = bootstrapResult.Config;
        _logger = new FileLogger(_runtimeLayout);
        _processSupervisor = new ProcessSupervisor(new WinwsProcessInspector(), _config);
        var executablePath = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "Zapret Manager.exe");
        _autostartService = new AutostartService(new CommandRunner(), executablePath);
        _logger.Info("Zapret Manager started.");

        _state = DetectState();
        _trayIcons = new TrayIconSet();
        _trayMenuActions = new TrayMenuActions(
            OnOpenMainWindow: ShowMainWindow,
            OnStart: () => RunZapretAction(ZapretActionKind.Start),
            OnStop: () => RunZapretAction(ZapretActionKind.Stop),
            OnRestart: () => RunZapretAction(ZapretActionKind.Restart),
            OnStopExisting: () => StopExistingZapretWithConfirmation(requireConfirmation: true),
            OnStrategySelected: SelectStrategy,
            OnAutoSelectStrategy: RunStrategyTests,
            OnClearDiscordCache: ClearDiscordCache,
            OnCheckUpdates: async () => await CheckUpdatesAsync(),
            OnOpenSettings: OpenSettings,
            OnExit: ExitApplication);

        var trayMenu = new ContextMenuStrip();
        trayMenu.Opening += OnTrayMenuOpening;
        _notifyIcon = new NotifyIcon
        {
            Text = "Zapret Manager",
            Icon = _trayIcons.Get(_state),
            ContextMenuStrip = trayMenu,
            Visible = true
        };
        _notifications = new TrayNotificationService(_notifyIcon);
        _notifyIcon.MouseClick += (_, args) =>
        {
            if (args.Button == MouseButtons.Left)
            {
                ShowMainWindow();
            }
        };

        _mainForm = BuildMainForm();
        _ = _mainForm.Handle;
        _singleInstance.StartActivationListener(_mainForm, ShowMainWindow, onExitRequested: ExitApplication);
        _mainForm.FormClosing += OnMainFormClosing;
        StartStatusMonitoring();

        if (startHidden)
        {
            ScheduleAutostartStrategy();
        }
        else
        {
            ShowMainWindow();
        }

        if (_config.CheckForUpdatesOnStartup)
        {
            // При входе в Windows сеть поднимается не сразу, а GitHub может открываться только с zapret —
            // поэтому в фоновом режиме проверяем после автозапуска стратегии.
            ScheduleAutomaticUpdateCheck(startHidden ? AutostartUpdateCheckDelay : TimeSpan.Zero);
        }
    }

    private MainForm BuildMainForm()
    {
        return new MainForm(
            getState: () => _state,
            getStrategies: GetStrategies,
            onStrategySelected: SelectStrategy,
            onStart: () => RunZapretAction(ZapretActionKind.Start),
            onStop: () => RunZapretAction(ZapretActionKind.Stop),
            onRestart: () => RunZapretAction(ZapretActionKind.Restart),
            onStopExisting: () => StopExistingZapretWithConfirmation(requireConfirmation: true),
            onCheckUpdates: async () => await CheckUpdatesAsync(),
            onAutoSelectStrategy: RunStrategyTests,
            onCancelAutoSelect: CancelStrategyTests,
            onOpenSettings: OpenSettings,
            getLastStrategyScan: () => _config.LastStrategyScan,
            getStatusHint: GetStatusHint,
            onClearDiscordCache: ClearDiscordCache,
            getRuntimeVersionText: GetRuntimeVersionText);
    }

    private void OnTrayMenuOpening(object? sender, CancelEventArgs args)
    {
        if (sender is not ContextMenuStrip menu)
        {
            return;
        }

        try
        {
            UpdateState();
            TrayMenuFactory.Populate(menu, _state, GetStrategies(), _trayMenuActions);
        }
        catch (Exception ex)
        {
            // Меню откроется пустым — лучше, чем упасть из-за сбоя детектирования.
            _logger.Error("Tray menu refresh failed.", ex);
            return;
        }

        // Пустое меню WinForms открывает с Cancel = true; после заполнения его нужно разрешить явно.
        args.Cancel = false;
    }

    private void OnMainFormClosing(object? sender, FormClosingEventArgs args)
    {
        // Завершение сеанса Windows нельзя отменять: иначе менеджер блокирует выключение компьютера.
        if (_isExiting)
        {
            return;
        }

        if (args.CloseReason == CloseReason.WindowsShutDown)
        {
            StopStatusMonitoring();
            return;
        }

        args.Cancel = true;
        _mainForm.Hide();
        if (args.CloseReason == CloseReason.UserClosing && !_config.TrayHintShown)
        {
            _config.TrayHintShown = true;
            _configService.Save(_config);
            _notifications.ShowInformation("Менеджер продолжает работать в трее. Выйти можно через меню иконки.");
        }
    }

    private ZapretStatus DetectZapretStatus()
    {
        return new ZapretDetectionService().Detect();
    }

    private ZapretState DetectState()
    {
        if (!RuntimeValidator.Validate(_runtimeLayout.RuntimeDirectory).IsComplete)
        {
            return ZapretState.RuntimeMissing;
        }

        if (_processSupervisor.ReconcileManagedProcess() is not null)
        {
            return ZapretState.Running;
        }

        var zapretStatus = DetectZapretStatus();
        return zapretStatus.ZapretServiceRunning || zapretStatus.WinwsProcessRunning
            ? ZapretState.External
            : ZapretState.Stopped;
    }

    private IReadOnlyList<StrategyInfo> GetStrategies()
    {
        return StrategyService.DiscoverStrategies(_runtimeLayout.RuntimeDirectory, _config.SelectedStrategy);
    }

    private StrategyInfo? GetSelectedStrategy()
    {
        return GetStrategies().FirstOrDefault(strategy => strategy.IsSelected);
    }

    private void RefreshUi()
    {
        UpdateState();
        _mainForm.RefreshState();
    }

    /// <summary>
    /// Результат действия и так виден в окне, если оно на переднем плане; иначе сообщаем через трей.
    /// </summary>
    private void ShowFeedback(string message)
    {
        if (_mainForm.Visible && Form.ActiveForm == _mainForm)
        {
            return;
        }

        _notifications.ShowInformation(message);
    }

    private async void RunZapretAction(ZapretActionKind action)
    {
        if (_exitInProgress)
        {
            return;
        }

        if (action == ZapretActionKind.Stop && !IsManagedProcessRunning())
        {
            var status = DetectZapretStatus();
            var message = status.ZapretServiceRunning || status.WinwsProcessRunning
                ? "zapret запущен не менеджером. Откройте окно и нажмите «Действия» в блоке «Управление»."
                : "Запущенный процесс не найден";
            _notifications.ShowInformation(message);
            RefreshUi();
            return;
        }

        if (!await _zapretActionGate.WaitAsync(0))
        {
            _notifications.ShowInformation("Другое действие с zapret уже выполняется.");
            return;
        }

        try
        {
            var result = await ExecuteZapretActionAsync(action);
            _logger.Info($"Zapret action {action}: {result.Outcome} — {result.Message}");
            RefreshUi();
            if (result.IsSuccess || result.Outcome == ZapretActionOutcome.Cancelled)
            {
                ShowFeedback(result.Message);
            }
            else
            {
                ThemedMessageBox.Show(result.Message, "Zapret Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Zapret action {action} failed.", ex);
            ThemedMessageBox.Show("Не удалось выполнить действие с zapret.\n\n" + ex.Message, "Zapret Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _zapretActionGate.Release();
        }
    }

    private Task<ZapretActionResponse> ExecuteZapretActionAsync(
        ZapretActionKind action,
        IProgress<StrategyAutoSelectionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Start/Stop синхронно ждут появления/завершения winws.exe (до 5–10 с) —
        // выполняем вне UI-потока, чтобы окно и трей не зависали.
        var executor = new ZapretActionExecutor(_configService, _runtimeLayout, _config);
        return Task.Run(() => executor.ExecuteAsync(action, progress, cancellationToken), CancellationToken.None);
    }

    private void StopExistingZapretWithConfirmation(bool requireConfirmation)
    {
        StopExistingZapretWithConfirmation(DetectZapretStatus(), requireConfirmation);
    }

    private async void StopExistingZapretWithConfirmation(ZapretStatus status, bool requireConfirmation)
    {
        if (!await _zapretActionGate.WaitAsync(0))
        {
            _notifications.ShowInformation("Другое действие с zapret уже выполняется.");
            return;
        }

        try
        {
            var stopService = CreateExistingZapretStopService();
            var runtimeProcesses = stopService.GetCurrentRuntimeProcesses();
            if (!status.ZapretServiceRunning && runtimeProcesses.Count == 0)
            {
                var message = status.WinwsProcessRunning
                    ? "Обнаружен внешний winws.exe из другой папки. Закройте его через исходный launcher или вручную, затем повторите действие. Менеджер не останавливает неизвестные процессы."
                    : "Активный service zapret или winws.exe из текущего runtime не найден.";
                ThemedMessageBox.Show(message, "Zapret Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (requireConfirmation)
            {
                var detected = BuildDetectedStopList(status, runtimeProcesses);
                var confirmation = ThemedMessageBox.ShowAction(
                    "Обнаружен внешний zapret, которым менеджер может безопасно управлять:\n" + detected,
                    "Zapret Manager",
                    "Остановить",
                    "Отмена",
                    MessageBoxIcon.Warning);
                if (confirmation != DialogResult.Yes)
                {
                    return;
                }
            }

            var result = await ExecuteZapretActionAsync(ZapretActionKind.StopExisting);
            _logger.Info($"Stop existing zapret: {result.Outcome} — {result.Message}");
            RefreshUi();
            if (result.IsSuccess || result.Outcome == ZapretActionOutcome.Cancelled)
            {
                ShowFeedback(result.Message);
                return;
            }

            ThemedMessageBox.Show(result.Message, "Zapret Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            _logger.Error("Stop existing zapret failed.", ex);
            ThemedMessageBox.Show(
                "Не удалось остановить внешний zapret.\n\n" + ex.Message,
                "Zapret Manager",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        finally
        {
            _zapretActionGate.Release();
        }
    }

    private ExistingZapretStopService CreateExistingZapretStopService()
    {
        return new ExistingZapretStopService(
            new CommandRunner(),
            new WinwsProcessInspector(),
            _runtimeLayout.RuntimeDirectory);
    }

    private static string BuildDetectedStopList(ZapretStatus status, IReadOnlyList<WinwsProcessInfo> runtimeProcesses)
    {
        var items = new List<string>();
        if (status.ZapretServiceRunning)
        {
            items.Add("- service zapret");
        }

        foreach (var process in runtimeProcesses)
        {
            items.Add($"- winws.exe (PID {process.ProcessId})");
        }

        return string.Join(Environment.NewLine, items);
    }

    private void OpenSettings()
    {
        using var form = new SettingsForm(_config, SetCheckForUpdatesOnStartup, SetStartWithWindows);
        if (_mainForm.Visible)
        {
            form.ShowDialog(_mainForm);
            return;
        }

        form.StartPosition = FormStartPosition.CenterScreen;
        form.ShowDialog();
    }

    private void SetCheckForUpdatesOnStartup(bool enabled)
    {
        _config.CheckForUpdatesOnStartup = enabled;
        _configService.Save(_config);
        _logger.Info("Setting changed: CheckForUpdatesOnStartup=" + enabled);
    }

    private bool SetStartWithWindows(bool enabled)
    {
        var autostartResult = _autostartService.SetEnabled(enabled);
        if (!autostartResult.IsSuccess)
        {
            _logger.Info("Autostart change failed: " + autostartResult.Message);
            ThemedMessageBox.Show(
                autostartResult.Message,
                "Zapret Manager",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        _config.StartWithWindows = enabled;
        _configService.Save(_config);
        _logger.Info("Setting changed: StartWithWindows=" + enabled);
        return true;
    }

    private void ExitApplication()
    {
        _ = ExitApplicationAsync();
    }

    private async Task ExitApplicationAsync()
    {
        if (_isExiting || _exitInProgress)
        {
            return;
        }

        _exitInProgress = true;
        try
        {
            var activeAutoSelection = _strategyAutoSelectionTask;
            _strategyAutoSelectionCancellation?.Cancel();
            if (activeAutoSelection is not null)
            {
                await activeAutoSelection;
            }

            await _zapretActionGate.WaitAsync();
            ManagedProcessShutdownResult shutdownResult;
            try
            {
                var shutdownService = new ManagedProcessShutdownService(
                    IsManagedProcessRunning,
                    () => ExecuteZapretActionAsync(ZapretActionKind.Stop));
                shutdownResult = await shutdownService.StopBeforeExitAsync();
            }
            finally
            {
                _zapretActionGate.Release();
            }

            if (!shutdownResult.CanExit)
            {
                RefreshUi();
                ThemedMessageBox.Show(
                    "Не удалось остановить zapret, поэтому менеджер остаётся открытым.\n\n" + shutdownResult.Message,
                    "Zapret Manager",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            CompleteExit();
        }
        catch (Exception ex)
        {
            _logger.Error("Application exit failed.", ex);
            ThemedMessageBox.Show(
                "Не удалось остановить zapret, поэтому менеджер остаётся открытым.\n\n" + ex.Message,
                "Zapret Manager",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        finally
        {
            if (!_isExiting)
            {
                _exitInProgress = false;
            }
        }
    }

    private bool IsManagedProcessRunning()
    {
        return _processSupervisor.ReconcileManagedProcess() is not null;
    }

    private void CompleteExit()
    {
        _isExiting = true;
        StopStatusMonitoring();
        _notifications.Dispose();
        _mainForm.Close();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopStatusMonitoring();
            _notifications.Dispose();
            _mainForm.Dispose();
            _notifyIcon.ContextMenuStrip?.Dispose();
            _notifyIcon.Dispose();
            _trayIcons.Dispose();
            _autostartTimer?.Dispose();
            _zapretActionGate.Dispose();
        }

        base.Dispose(disposing);
    }
}
