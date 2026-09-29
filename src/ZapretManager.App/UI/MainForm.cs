using System.Diagnostics;
using ZapretManager.App.Core;
using ZapretManager.App.Services;

namespace ZapretManager.App.UI;

public sealed class MainForm : Form
{
    internal const int UptimeRefreshIntervalMilliseconds = 1_000;
    private const int WindowWidth = 490;
    private const int WindowHeight = 456;
    private const int LayoutMargin = 20;
    private const int CardWidth = WindowWidth - LayoutMargin * 2; // 450
    private const int CardInset = 14;
    private const int ContentWidth = CardWidth - CardInset * 2; // 422
    private const int HeaderTop = 16;
    private const int HeaderControlSize = 32;
    private const int HeaderTextLeft = LayoutMargin + HeaderControlSize + 10;

    private readonly IZapretManagerCommands _commands;
    private readonly Label _runtimeVersionLabel;
    private readonly PowerStatusButton _powerButton;
    private readonly Button _restartButton;
    private readonly StrategyPicker _strategyPicker;
    private readonly Button _autoSelectButton;
    private readonly Button _cancelAutoSelectButton;
    private readonly ScanProgressBar _strategyProgress;
    private readonly Label _strategyElapsedLabel;
    private readonly Label _strategyResultLabel;
    private readonly Label _strategyTopLabel;
    private readonly StrategyResultsList _strategyResultsList;
    private readonly System.Windows.Forms.Timer _strategyElapsedTimer = new() { Interval = 1_000 };
    private readonly System.Windows.Forms.Timer _uptimeRefreshTimer = new() { Interval = UptimeRefreshIntervalMilliseconds };
    private readonly ThemedToolTip _toolTip = new();
    private bool _loadingStrategies;
    private bool _isStrategyAutoSelectionRunning;
    private bool _strategySelectionEnabled = true;
    private Stopwatch? _strategyElapsed;

    public MainForm(IZapretManagerCommands commands)
    {
        _commands = commands;

        Text = "Zapret Manager";
        Icon = AppAssets.LoadWindowIcon();
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(WindowWidth, WindowHeight);
        ClientSize = new Size(WindowWidth, WindowHeight);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        ShowIcon = false;
        UiTheme.ApplyWindow(this);

        // Шапка: знак в плашке той же формы, что кнопки справа, название и версия Flowseal под ним.
        var logoTile = new AppLogoTile
        {
            Name = "AppLogo",
            Location = new Point(LayoutMargin, HeaderTop),
            Size = new Size(HeaderControlSize, HeaderControlSize)
        };

        var titleLabel = new Label
        {
            Name = "AppTitleLabel",
            Text = "Zapret Manager",
            AutoSize = false,
            Location = new Point(HeaderTextLeft, HeaderTop - 2),
            Size = new Size(200, 19),
            TextAlign = ContentAlignment.MiddleLeft
        };
        UiTheme.StyleLabel(titleLabel, UiTheme.Text, 10.5F);
        titleLabel.Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Regular, GraphicsUnit.Point);

        _runtimeVersionLabel = new Label
        {
            Name = "RuntimeVersionLabel",
            AutoSize = false,
            Location = new Point(HeaderTextLeft, HeaderTop + 17),
            Size = new Size(200, 16),
            TextAlign = ContentAlignment.MiddleLeft,
            AccessibleName = "Установленная версия zapret (Flowseal)"
        };
        UiTheme.StyleLabel(_runtimeVersionLabel, UiTheme.MutedText, 8.5F);
        _toolTip.SetToolTip(_runtimeVersionLabel, "Установленная версия zapret (Flowseal)");

        var checkUpdatesButton = CreateIconButton(UiIconKind.Update, 398, HeaderTop, commands.CheckUpdates, "Проверить обновления");
        checkUpdatesButton.Name = "CheckUpdatesButton";
        checkUpdatesButton.AccessibleName = "Проверить обновления";
        checkUpdatesButton.AccessibleDescription = "Проверить обновления Zapret Manager и zapret (Flowseal).";

        var settingsButton = CreateIconButton(UiIconKind.Settings, 438, HeaderTop, commands.OpenSettings, "Настройки");
        settingsButton.Name = "SettingsButton";
        settingsButton.AccessibleName = "Настройки";
        settingsButton.AccessibleDescription = "Открыть настройки менеджера.";

        var controlGroup = new ThemedSectionPanel
        {
            Name = "ControlSection",
            Title = "Управление",
            Location = new Point(LayoutMargin, 68),
            Size = new Size(CardWidth, 118)
        };
        UiTheme.StyleSection(controlGroup);

        _powerButton = new PowerStatusButton
        {
            Name = "PowerButton",
            Location = new Point(CardInset, 34),
            Size = new Size(ContentWidth, 68)
        };
        _powerButton.Click += (_, _) =>
        {
            switch (_powerButton.StatusState)
            {
                case ZapretState.Running:
                    commands.StopZapret();
                    break;
                case ZapretState.Stopped:
                    commands.StartZapret();
                    break;
                case ZapretState.External:
                    commands.StopExistingZapret();
                    break;
            }
        };

        // Перезапуск имеет смысл только для работающего zapret, поэтому кнопка видна лишь во включённом состоянии.
        // Она стоит в строке заголовка карточки: когда её нет, под индикатором не остаётся дыры,
        // а сам индикатор не сдвигается под курсором при включении и выключении.
        _restartButton = CreateButton("↻  Перезапустить", CardWidth - CardInset - 128, 5, commands.RestartZapret, width: 128, UiButtonKind.Subtle);
        _restartButton.Name = "RestartButton";
        _restartButton.Height = 24;
        _restartButton.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point);
        _restartButton.AccessibleName = "Перезапустить zapret";
        _restartButton.AccessibleDescription = "Перезапустить управляемый процесс zapret с выбранной стратегией.";
        _toolTip.SetToolTip(_restartButton, _restartButton.AccessibleDescription);

        controlGroup.Controls.Add(_powerButton);
        controlGroup.Controls.Add(_restartButton);

        var strategyGroup = new ThemedSectionPanel
        {
            Name = "StrategySection",
            Title = "Стратегия",
            Location = new Point(LayoutMargin, 198),
            Size = new Size(CardWidth, 218)
        };
        UiTheme.StyleSection(strategyGroup);

        _strategyPicker = new StrategyPicker
        {
            Name = "StrategyComboBox",
            Location = new Point(CardInset, 32),
            Size = new Size(260, 32),
            AccessibleName = "Стратегия zapret",
            AccessibleDescription = "Выбор стратегии zapret. Используйте стрелки вверх и вниз, Enter и Escape."
        };
        _strategyPicker.SelectedItemChanged += (_, _) =>
        {
            if (_loadingStrategies || _strategyPicker.SelectedItem is not StrategyInfo strategy)
            {
                return;
            }

            _commands.SelectStrategy(strategy);
        };

        _autoSelectButton = CreateButton("Автовыбор", 284, 32, commands.RunStrategyAutoSelection, width: 152);
        _autoSelectButton.Name = "AutoSelectButton";
        _toolTip.SetToolTip(_autoSelectButton, "Проверить стратегии и автоматически выбрать лучшую.");

        // Кнопка «Остановить» занимает место «Автовыбор» на время сканирования.
        _cancelAutoSelectButton = CreateButton("Остановить", 284, 32, commands.CancelStrategyAutoSelection, width: 152, UiButtonKind.Danger);
        _cancelAutoSelectButton.Name = "CancelAutoSelectButton";
        _cancelAutoSelectButton.Visible = false;
        _toolTip.SetToolTip(_cancelAutoSelectButton, "Остановить текущий автовыбор стратегии.");

        _strategyProgress = new ScanProgressBar
        {
            Name = "StrategyAutoSelectionProgress",
            Location = new Point(CardInset, 78),
            Size = new Size(ContentWidth, 42),
            Visible = false
        };
        _strategyProgress.HideBar();

        _strategyElapsedLabel = new Label
        {
            Name = "StrategyAutoSelectionElapsedLabel",
            Text = "Прошло: 00:00",
            AutoSize = false,
            Location = new Point(CardInset, 124),
            Size = new Size(ContentWidth, 14),
            TextAlign = ContentAlignment.TopRight,
            Visible = false
        };
        UiTheme.StyleLabel(_strategyElapsedLabel, UiTheme.MutedText, 8F);
        _strategyElapsedTimer.Tick += (_, _) => UpdateElapsedLabel();

        _strategyResultLabel = new Label
        {
            Name = "StrategyAutoSelectionStatusLabel",
            Text = string.Empty,
            AutoSize = false,
            AutoEllipsis = true,
            Location = new Point(CardInset, 78),
            Size = new Size(ContentWidth, 15)
        };
        UiTheme.StyleLabel(_strategyResultLabel, UiTheme.MutedText, 9F);

        _strategyTopLabel = new Label
        {
            Name = "StrategyAutoSelectionTopLabel",
            Text = "Лучшие стратегии появятся после автовыбора.",
            AutoSize = false,
            Location = new Point(CardInset, 78),
            Size = new Size(ContentWidth, 16)
        };
        UiTheme.StyleLabel(_strategyTopLabel, UiTheme.MutedText, 8F, FontStyle.Bold);

        _strategyResultsList = new StrategyResultsList
        {
            Name = "StrategyAutoSelectionTopList",
            Location = new Point(CardInset, 100),
            Size = new Size(ContentWidth, 80),
            Visible = false
        };

        strategyGroup.Controls.Add(_strategyPicker);
        strategyGroup.Controls.Add(_autoSelectButton);
        strategyGroup.Controls.Add(_cancelAutoSelectButton);
        strategyGroup.Controls.Add(_strategyProgress);
        strategyGroup.Controls.Add(_strategyElapsedLabel);
        strategyGroup.Controls.Add(_strategyResultLabel);
        strategyGroup.Controls.Add(_strategyTopLabel);
        strategyGroup.Controls.Add(_strategyResultsList);

        // Кеш Discord чаще всего мешает сразу после смены стратегии, поэтому действие подписано
        // и стоит под блоком стратегии, а не безымянной иконкой в шапке.
        var clearDiscordCacheButton = CreateButton(
            "Discord не грузится? Очистить кеш",
            (WindowWidth - 260) / 2,
            423,
            commands.ClearDiscordCache,
            width: 260,
            UiButtonKind.Subtle);
        clearDiscordCacheButton.Name = "ClearDiscordCacheButton";
        clearDiscordCacheButton.Height = 26;
        clearDiscordCacheButton.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point);
        clearDiscordCacheButton.AccessibleName = "Очистить кеш Discord";
        clearDiscordCacheButton.AccessibleDescription = "Закрыть Discord и удалить его кеш — помогает, если после смены стратегии Discord не грузится.";
        _toolTip.SetToolTip(clearDiscordCacheButton, clearDiscordCacheButton.AccessibleDescription);

        Controls.Add(logoTile);
        Controls.Add(titleLabel);
        Controls.Add(_runtimeVersionLabel);
        Controls.Add(checkUpdatesButton);
        Controls.Add(settingsButton);
        Controls.Add(controlGroup);
        Controls.Add(strategyGroup);
        Controls.Add(clearDiscordCacheButton);

        RefreshState();
        _uptimeRefreshTimer.Tick += (_, _) =>
        {
            if (Visible)
            {
                RefreshUptimeHint();
            }
        };
        _uptimeRefreshTimer.Start();
    }

    private bool _isAutoSelectMode = true;

    /// <summary>Для тестов: виден ли сейчас «Автовыбор» (иначе — «Остановить»).</summary>
    internal bool IsAutoSelectMode => _isAutoSelectMode;

    private void SetAutoSelectMode(bool isAutoSelect)
    {
        _isAutoSelectMode = isAutoSelect;
        _autoSelectButton.Visible = isAutoSelect;
        _cancelAutoSelectButton.Visible = !isAutoSelect;
        if (!isAutoSelect)
        {
            _cancelAutoSelectButton.BringToFront();
        }
    }

    public void SetStrategySelectionEnabled(bool enabled)
    {
        _strategySelectionEnabled = enabled;
        RunOnUiThread(() => _strategyPicker.Enabled = enabled && _strategyPicker.Items.Count > 0);
    }

    public void RefreshState()
    {
        var state = _commands.GetState();
        _powerButton.StatusState = state;
        _powerButton.Enabled = state != ZapretState.RuntimeMissing;
        _restartButton.Visible = state == ZapretState.Running;
        _runtimeVersionLabel.Text = _commands.GetRuntimeVersionText();
        RefreshUptimeHint();

        var strategies = _commands.GetStrategies();
        var selected = strategies.FirstOrDefault(strategy => strategy.IsSelected);

        _loadingStrategies = true;
        try
        {
            _strategyPicker.SetItems(strategies, selected);
            _strategyPicker.Enabled = _strategySelectionEnabled && strategies.Count > 0;
        }
        finally
        {
            _loadingStrategies = false;
        }

        if (!_isStrategyAutoSelectionRunning)
        {
            SetStrategyResults(GetLastScanRows(_commands.GetLastStrategyScan()));
            LayoutStrategyResults(showCompletionDetails: !string.IsNullOrEmpty(_strategyResultLabel.Text));
        }
    }

    public void BeginStrategyAutoSelection(int totalStrategies)
    {
        RunOnUiThread(() =>
        {
            _isStrategyAutoSelectionRunning = true;
            _strategyElapsed?.Stop();
            _strategyElapsed = Stopwatch.StartNew();
            _strategyElapsedTimer.Start();
            _strategyProgress.SetProgress(0, totalStrategies, totalStrategies > 0 ? "Готовимся проверить стратегии..." : string.Empty);
            _strategyProgress.ShowBar();
            _strategyResultLabel.Text = string.Empty;
            _strategyTopLabel.Visible = false;
            _strategyResultsList.Visible = false;
            _strategyElapsedLabel.Visible = true;
            _strategyElapsedLabel.Location = new Point(CardInset, 124);
            _strategyElapsedLabel.Size = new Size(ContentWidth, 14);
            UpdateElapsedLabel();
            SetAutoSelectMode(false);
        });
    }

    public void UpdateStrategyAutoSelectionProgress(StrategyAutoSelectionProgress progress)
    {
        RunOnUiThread(() =>
        {
            var currentItem = string.IsNullOrEmpty(progress.CurrentStrategyName)
                ? progress.Message
                : $"Проверяется: {progress.CurrentStrategyName}";
            _strategyProgress.SetProgress(progress.CompletedStrategies, progress.TotalStrategies, currentItem);
            _strategyProgress.ShowBar();
        });
    }

    /// <summary>Меняет только подпись прогресса, не трогая счётчики (например, при остановке).</summary>
    public void SetStrategyAutoSelectionStatus(string message)
    {
        RunOnUiThread(() => _strategyProgress.SetProgress(_strategyProgress.Completed, _strategyProgress.Total, message));
    }

    public void ShowStrategyAutoSelectionResult(StrategyAutoSelectionResult result)
    {
        RunOnUiThread(() =>
        {
            _isStrategyAutoSelectionRunning = false;
            StopElapsedTimer();
            _strategyProgress.HideBar();
            SetStrategyResultMessage(result.Message, result.Notes);
            SetStrategyResults(result.IsSuccess
                ? result.TopStrategies.Select(StrategyCheckFormatter.ToRow)
                : GetLastScanRows(_commands.GetLastStrategyScan()));
            LayoutStrategyResults(showCompletionDetails: true);
            SetAutoSelectMode(true);
        });
    }

    public void FinishStrategyAutoSelectionWithError(string message)
    {
        RunOnUiThread(() =>
        {
            _isStrategyAutoSelectionRunning = false;
            StopElapsedTimer();
            _strategyProgress.HideBar();
            SetStrategyResultMessage(message, []);
            SetStrategyResults(GetLastScanRows(_commands.GetLastStrategyScan()));
            LayoutStrategyResults(showCompletionDetails: true);
            SetAutoSelectMode(true);
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
            _strategyElapsedTimer.Dispose();
            _uptimeRefreshTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void RunOnUiThread(Action action)
    {
        if (IsHandleCreated && InvokeRequired)
        {
            BeginInvoke(action);
            return;
        }

        action();
    }

    private void UpdateElapsedLabel()
    {
        var elapsed = _strategyElapsed?.Elapsed ?? TimeSpan.Zero;
        _strategyElapsedLabel.Text = $"Прошло: {(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}";
    }

    private void StopElapsedTimer()
    {
        _strategyElapsedTimer.Stop();
        _strategyElapsed?.Stop();
        UpdateElapsedLabel();
        _strategyElapsedLabel.Visible = true;
    }

    /// <summary>Итог — одной строкой; наблюдения прогона (что заблокировано, DNS, ничьи) — в подсказке по наведению.</summary>
    private void SetStrategyResultMessage(string message, IReadOnlyList<string> notes)
    {
        var hasNotes = notes.Count > 0;
        _strategyResultLabel.Text = hasNotes ? "ⓘ " + message : message;
        _strategyResultLabel.Cursor = hasNotes ? Cursors.Help : Cursors.Default;
        _toolTip.SetToolTip(
            _strategyResultLabel,
            hasNotes
                ? "Итоги проверки" + Environment.NewLine +
                  string.Join(Environment.NewLine + Environment.NewLine, notes.Select(note => "• " + note))
                : null);
    }

    private void SetStrategyResults(IEnumerable<StrategyResultsList.ResultRow> rows)
    {
        var materializedRows = rows.ToArray();
        _strategyResultsList.SetRows(materializedRows);
        var hasItems = _strategyResultsList.Items.Count > 0;
        _strategyTopLabel.Text = hasItems
            ? "Лучшие стратегии последнего сканирования:"
            : "Лучшие стратегии появятся после автовыбора.";
        _strategyTopLabel.Visible = true;
        _strategyResultsList.Visible = hasItems;
    }

    private static IEnumerable<StrategyResultsList.ResultRow> GetLastScanRows(LastStrategyScanResult? result)
    {
        if (result is null || result.Strategies.Count == 0)
        {
            return [];
        }

        return result.Strategies.Take(3).Select(StrategyCheckFormatter.ToRow).ToArray();
    }

    internal void RefreshUptimeHint()
    {
        _powerButton.DetailText = _commands.GetStatusHint();
    }

    private void LayoutStrategyResults(bool showCompletionDetails)
    {
        _strategyProgress.HideBar();
        _strategyResultsList.BringToFront();
        if (showCompletionDetails)
        {
            _strategyResultLabel.Location = new Point(CardInset, 78);
            _strategyResultLabel.Size = new Size(ContentWidth - 110, 15);
            _strategyElapsedLabel.Location = new Point(334, 79);
            _strategyElapsedLabel.Size = new Size(102, 14);
            _strategyTopLabel.Location = new Point(CardInset, 100);
            _strategyResultsList.Location = new Point(CardInset, 122);
            return;
        }

        _strategyResultLabel.Location = new Point(CardInset, 78);
        _strategyResultLabel.Size = new Size(ContentWidth, 15);
        _strategyElapsedLabel.Location = new Point(CardInset, 124);
        _strategyElapsedLabel.Size = new Size(ContentWidth, 14);
        _strategyTopLabel.Location = new Point(CardInset, 78);
        _strategyResultsList.Location = new Point(CardInset, 100);
    }

    private Button CreateIconButton(UiIconKind iconKind, int x, int y, Action onClick, string tooltip)
    {
        var button = new ThemedIconButton
        {
            IconKind = iconKind,
            Location = new Point(x, y),
            Size = new Size(32, 32),
            Cursor = Cursors.Hand
        };
        UiTheme.StyleButton(button, UiButtonKind.Ghost);
        button.Click += (_, _) => onClick();
        _toolTip.SetToolTip(button, tooltip);
        return button;
    }

    private static Button CreateButton(string text, int x, int y, Action onClick, int width = 110, UiButtonKind kind = UiButtonKind.Secondary)
    {
        var button = new ThemedButton
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, 32),
            Cursor = Cursors.Hand
        };
        UiTheme.StyleButton(button, kind);
        button.Click += (_, _) => onClick();
        return button;
    }
}
