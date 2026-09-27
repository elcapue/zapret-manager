using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using ZapretManager.App.Core;
using ZapretManager.App.Services;
using ZapretManager.App.UI;

namespace ZapretManager.Tests;

public sealed class MainFormTests
{
    [Fact]
    public void Constructor_BuildsControlAndStrategyBlocks()
    {
        using var form = CreateForm(ZapretState.Stopped);

        var controlGroup = GetAllControls(form).OfType<Panel>().Single(panel => panel.Name == "ControlSection");
        var strategyGroup = GetAllControls(form).OfType<Panel>().Single(panel => panel.Name == "StrategySection");

        Assert.True(controlGroup.Top < strategyGroup.Top);
        Assert.Contains(controlGroup.Controls.OfType<Label>(), label => label.Text == UiTheme.ToEyebrowText("Управление"));
        Assert.Contains(strategyGroup.Controls.OfType<Label>(), label => label.Text == UiTheme.ToEyebrowText("Стратегия"));
        var powerButton = GetAllControls(form).OfType<PowerStatusButton>().Single(button => button.Name == "PowerButton");
        Assert.Equal(ZapretState.Stopped, powerButton.StatusState);
        Assert.Same(controlGroup, powerButton.Parent);
        Assert.Contains(controlGroup.Controls.OfType<Button>(), button => button.Name == "RestartButton");
        Assert.Contains(strategyGroup.Controls.OfType<Button>(), button => button.Text == "Автовыбор");
        Assert.Contains(strategyGroup.Controls.OfType<StrategyPicker>(), picker => picker.Name == "StrategyComboBox");
        Assert.False(form.ShowIcon);
    }

    [Fact]
    public void Constructor_HeaderShowsLogoTileInLineWithIconButtons()
    {
        using var form = CreateForm(ZapretState.Stopped);

        var logo = form.Controls.OfType<AppLogoTile>().Single(tile => tile.Name == "AppLogo");
        var title = form.Controls.OfType<Label>().Single(label => label.Name == "AppTitleLabel");
        var settings = form.Controls.OfType<Button>().Single(button => button.Name == "SettingsButton");

        Assert.Equal("Zapret Manager", title.Text);
        Assert.Equal(settings.Size, logo.Size);
        Assert.Equal(settings.Top, logo.Top);
        Assert.True(title.Left > logo.Right);
        Assert.False(logo.TabStop);
    }

    [Fact]
    public void Constructor_WithoutPreviousScan_ShowsFirstRunHint()
    {
        using var form = CreateForm(ZapretState.Stopped);

        var topLabel = GetAllControls(form).OfType<Label>().Single(label => label.Name == "StrategyAutoSelectionTopLabel");
        var resultLabel = GetAllControls(form).OfType<Label>().Single(label => label.Name == "StrategyAutoSelectionStatusLabel");

        Assert.Equal("Лучшие стратегии появятся после автовыбора.", topLabel.Text);
        Assert.Equal(string.Empty, resultLabel.Text);
    }

    [Fact]
    public void Constructor_SettingsAndUpdatesAreIconButtonsWithAccessibleNames()
    {
        using var form = CreateForm(ZapretState.Stopped);

        var settings = GetAllControls(form).OfType<Button>().Single(button => button.Name == "SettingsButton");
        var updates = GetAllControls(form).OfType<Button>().Single(button => button.Name == "CheckUpdatesButton");

        var controlSection = GetAllControls(form).OfType<Panel>().Single(panel => panel.Name == "ControlSection");

        Assert.All([settings, updates], button =>
        {
            Assert.Equal(string.Empty, button.Text);
            Assert.False(string.IsNullOrWhiteSpace(button.AccessibleName));
            Assert.True(button.Bottom <= controlSection.Top);
        });
        Assert.True(updates.Right <= settings.Left);
    }

    [Fact]
    public void RefreshState_ShowsRuntimeVersionInHeaderInsteadOfPowerButton()
    {
        using var form = new MainForm(
            getState: () => ZapretState.Stopped,
            getStrategies: () => Array.Empty<StrategyInfo>(),
            onStrategySelected: _ => { },
            onStart: () => { },
            onStop: () => { },
            onRestart: () => { },
            onStopExisting: () => { },
            onCheckUpdates: () => { },
            onAutoSelectStrategy: () => { },
            onCancelAutoSelect: () => { },
            onOpenSettings: () => { },
            getRuntimeVersionText: () => "Flowseal 1.10.3");

        var version = form.Controls.OfType<Label>().Single(label => label.Name == "RuntimeVersionLabel");
        var title = form.Controls.OfType<Label>().Single(label => label.Name == "AppTitleLabel");
        var power = GetAllControls(form).OfType<PowerStatusButton>().Single();

        Assert.Equal("Flowseal 1.10.3", version.Text);
        Assert.Equal(title.Left, version.Left);
        Assert.True(version.Top >= title.Bottom - 2);
        Assert.Equal(string.Empty, power.DetailText);
    }

    [Fact]
    public void Constructor_ClearDiscordCacheIsLabeledFooterActionUnderStrategyBlock()
    {
        var clicked = false;
        using var form = CreateForm(ZapretState.Stopped, onClearDiscordCache: () => clicked = true);

        var clear = form.Controls.OfType<Button>().Single(button => button.Name == "ClearDiscordCacheButton");
        var strategySection = GetAllControls(form).OfType<Panel>().Single(panel => panel.Name == "StrategySection");
        // PerformClick у непоказанной формы не срабатывает (кнопка не CanSelect).
        typeof(Control)
            .GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(clear, [EventArgs.Empty]);

        Assert.Contains("Discord", clear.Text);
        Assert.True(clear.Top > strategySection.Bottom);
        Assert.True(clicked);
    }

    [Fact]
    public void Constructor_RestartIsSubtleActionInControlSectionHeader()
    {
        using var form = CreateForm(ZapretState.Running);

        var controlSection = GetAllControls(form).OfType<Panel>().Single(panel => panel.Name == "ControlSection");
        var restart = controlSection.Controls.OfType<Button>().Single(button => button.Name == "RestartButton");
        var power = controlSection.Controls.OfType<PowerStatusButton>().Single();
        var title = controlSection.Controls.OfType<Label>().Single(label => label.Name == "SectionTitleLabel");

        Assert.True(restart.Bottom <= power.Top);
        Assert.True(title.Right < restart.Left);
        Assert.True(restart.TabStop);
        Assert.False(string.IsNullOrWhiteSpace(restart.AccessibleName));
    }

    [Theory]
    [InlineData(ZapretState.Running, true)]
    [InlineData(ZapretState.Stopped, false)]
    [InlineData(ZapretState.External, false)]
    [InlineData(ZapretState.RuntimeMissing, false)]
    public void RefreshState_ShowsRestartOnlyWhileRunning(ZapretState state, bool expectedVisible)
    {
        using var form = CreateForm(state);
        var restart = GetAllControls(form).OfType<Button>().Single(button => button.Name == "RestartButton");

        Assert.Equal(expectedVisible, IsOwnVisible(restart));
    }

    [Theory]
    [InlineData(ZapretState.Running, "Выключить zapret", true)]
    [InlineData(ZapretState.Stopped, "Включить zapret", true)]
    [InlineData(ZapretState.External, "Действия с внешним zapret", true)]
    [InlineData(ZapretState.RuntimeMissing, "zapret недоступен", false)]
    public void RefreshState_ReflectsAllStatesInCombinedPowerStatusButton(
        ZapretState state,
        string expectedAccessibleName,
        bool expectedEnabled)
    {
        using var form = CreateForm(state);

        var powerButton = GetAllControls(form).OfType<PowerStatusButton>().Single();

        Assert.Equal(state, powerButton.StatusState);
        Assert.Equal(expectedAccessibleName, powerButton.AccessibleName);
        Assert.Equal(expectedEnabled, powerButton.Enabled);
    }

    [Fact]
    public void RefreshState_ShowsProvidedStatusHintInsidePowerButton()
    {
        using var form = CreateForm(ZapretState.Running, statusHint: "2:17:08 · general (ALT2)");
        var power = GetAllControls(form).OfType<PowerStatusButton>().Single();

        Assert.Equal("2:17:08 · general (ALT2)", power.DetailText);
        Assert.Equal("Включено. 2:17:08 · general (ALT2)", power.AccessibleDescription);

    }

    [Fact]
    public void PowerButton_ExternalClickUsesExistingExternalActionFlow()
    {
        var externalActionCalls = 0;
        using var form = CreateForm(
            ZapretState.External,
            onStopExisting: () => externalActionCalls++);
        var power = GetAllControls(form).OfType<PowerStatusButton>().Single();

        typeof(Button)
            .GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(power, [EventArgs.Empty]);

        Assert.Equal(1, externalActionCalls);
    }

    [Fact]
    public void RefreshUptimeHint_DoesNotRunHeavyStatusRefresh()
    {
        var stateCalls = 0;
        var hintCalls = 0;
        using var form = CreateForm(
            ZapretState.Running,
            stateProvider: () =>
            {
                stateCalls++;
                return ZapretState.Running;
            },
            statusHintProvider: () =>
            {
                hintCalls++;
                return "00:01";
            });
        stateCalls = 0;
        hintCalls = 0;

        form.RefreshUptimeHint();

        Assert.Equal(0, stateCalls);
        Assert.Equal(1, hintCalls);
    }

    [Fact]
    public void BeginStrategyAutoSelection_EnablesCancelButtonAndDisablesAutoSelectButton()
    {
        using var form = CreateForm(ZapretState.Stopped);

        form.BeginStrategyAutoSelection(totalStrategies: 4);

        var cancel = GetAllControls(form).OfType<Button>().Single(button => button.Name == "CancelAutoSelectButton");

        Assert.False(form.IsAutoSelectMode);
        Assert.Equal("Остановить", cancel.Text);
        var elapsed = GetAllControls(form).OfType<Label>()
            .Single(label => label.Name == "StrategyAutoSelectionElapsedLabel");
        Assert.Equal("Прошло: 00:00", elapsed.Text);
        var progress = GetAllControls(form).OfType<ScanProgressBar>().Single();
        Assert.True(progress.IsShown);
    }

    [Fact]
    public void ShowStrategyAutoSelectionResult_DisablesCancelButtonAndEnablesAutoSelectButton()
    {
        using var form = CreateForm(ZapretState.Stopped);
        form.BeginStrategyAutoSelection(totalStrategies: 1);
        var result = new StrategyAutoSelectionResult(
            StrategyAutoSelectionStatus.Completed,
            new StrategyInfo("general.bat", "C:/runtime/general.bat", false),
            Array.Empty<StrategyTestSummary>(),
            "done");

        form.ShowStrategyAutoSelectionResult(result);

        Assert.True(form.IsAutoSelectMode);
        var elapsed = GetAllControls(form).OfType<Label>()
            .Single(label => label.Name == "StrategyAutoSelectionElapsedLabel");
        Assert.StartsWith("Прошло: ", elapsed.Text);
    }

    [Fact]
    public void StrategyAutoSelectionProgress_UpdatesProgressBarInsideStrategyBlock()
    {
        using var form = CreateForm(ZapretState.Stopped);

        form.BeginStrategyAutoSelection(totalStrategies: 4);
        form.UpdateStrategyAutoSelectionProgress(new StrategyAutoSelectionProgress(CompletedStrategies: 2, TotalStrategies: 4, CurrentStrategyName: "general (ALT).bat", Message: "Проверяется 3/4: general (ALT).bat"));

        var progress = GetAllControls(form).OfType<ScanProgressBar>().Single(bar => bar.Name == "StrategyAutoSelectionProgress");
        var strategyGroup = GetAllControls(form).OfType<Panel>().Single(panel => panel.Name == "StrategySection");

        Assert.True(progress.IsShown);
        Assert.Equal(2, progress.Completed);
        Assert.Equal(4, progress.Total);
        Assert.Equal(strategyGroup, progress.Parent);
    }

    [Fact]
    public void ShowStrategyAutoSelectionResult_ShowsTopThreeStrategies()
    {
        using var form = CreateForm(ZapretState.Stopped);
        var result = new StrategyAutoSelectionResult(
            StrategyAutoSelectionStatus.Completed,
            new StrategyInfo("general2.bat", "C:/runtime/general2.bat", false),
            new[]
            {
                Summary("general2.bat", httpOk: 31, httpError: 5, youTubeOk: 10, discordOk: 9, latencyMs: 140),
                Summary("general3.bat", httpOk: 29, httpError: 7, youTubeOk: 9, discordOk: 8),
                Summary("general.bat", httpOk: 28, httpError: 8, youTubeOk: 8, discordOk: 8),
                Summary("general4.bat", httpOk: 1, httpError: 35, youTubeOk: 0, discordOk: 0)
            },
            "Лучшая стратегия: general2",
            ["Без zapret не открываются 2 из 12 сайтов."]);

        form.ShowStrategyAutoSelectionResult(result);

        var topLabel = GetAllControls(form).OfType<Label>().Single(label => label.Name == "StrategyAutoSelectionTopLabel");
        var topList = GetAllControls(form).OfType<StrategyResultsList>().Single(list => list.Name == "StrategyAutoSelectionTopList");
        var resultLabel = GetAllControls(form).OfType<Label>().Single(label => label.Name == "StrategyAutoSelectionStatusLabel");

        Assert.Equal("ⓘ Лучшая стратегия: general2", resultLabel.Text);
        Assert.Equal(Cursors.Help, resultLabel.Cursor);
        Assert.Equal("Лучшие стратегии последнего сканирования:", topLabel.Text);
        Assert.Equal(["general2.bat", "general3.bat", "general.bat"], topList.Items);
        Assert.DoesNotContain("Ping", topList.Text);
        Assert.Equal(
            ["86% · 140 мс", "81%", "78%"],
            topList.Rows.Select(row => row.Metric));
        Assert.Equal(
            "general2\nПрошли\t31 из 36\nYouTube\t10 из 12\nDiscord\t9 из 12\nДругие сайты\t12 из 12\nЗадержка\t140 мс",
            topList.Rows[0].Details.Replace("\r\n", "\n"));
        Assert.True(topList.Parent!.ClientRectangle.Contains(topList.Bounds));
    }

    [Theory]
    [InlineData(ZapretState.Running)]
    [InlineData(ZapretState.Stopped)]
    [InlineData(ZapretState.External)]
    public void Layout_KeepsEveryVisibleControlInsideItsContainer(ZapretState state)
    {
        using var form = CreateForm(state, statusHint: "12:34:56 · general (FAKE TLS AUTO ALT3)");

        foreach (var control in GetAllControls(form).Where(IsOwnVisible))
        {
            var container = control.Parent!;
            Assert.True(
                container.ClientRectangle.Contains(control.Bounds),
                $"{control.Name} ({control.Bounds}) выходит за границы {container.Name} ({container.ClientRectangle})");
        }
    }

    [Fact]
    public void Constructor_ShowsPersistedLastScanAfterRestart()
    {
        var lastScan = new LastStrategyScanResult
        {
            ScannedAtUtc = new DateTimeOffset(2026, 8, 27, 10, 30, 0, TimeSpan.Zero),
            Strategies =
            [
                new LastStrategyScanSummary
                {
                    StrategyFileName = "general2.bat",
                    HttpOk = 8,
                    HttpError = 1,
                    YouTubeHttpOk = 3,
                    YouTubeHttpAttempts = 3,
                    DiscordHttpOk = 2,
                    DiscordHttpAttempts = 3
                }
            ]
        };

        using var form = CreateForm(ZapretState.Stopped, lastScan);
        var topLabel = GetAllControls(form).OfType<Label>()
            .Single(label => label.Name == "StrategyAutoSelectionTopLabel");
        var topList = GetAllControls(form).OfType<StrategyResultsList>()
            .Single(list => list.Name == "StrategyAutoSelectionTopList");

        Assert.Equal("Лучшие стратегии последнего сканирования:", topLabel.Text);
        Assert.Equal(["general2.bat"], topList.Items);
        Assert.DoesNotContain("Ping", topList.Text);
        Assert.Equal("89%", Assert.Single(topList.Rows).Metric);
    }

    private static MainForm CreateForm(
        ZapretState state,
        LastStrategyScanResult? lastScan = null,
        string statusHint = "",
        Action? onStopExisting = null,
        Func<ZapretState>? stateProvider = null,
        Func<string>? statusHintProvider = null,
        Action? onClearDiscordCache = null)
    {
        return new MainForm(
            getState: stateProvider ?? (() => state),
            getStrategies: () => Array.Empty<StrategyInfo>(),
            onStrategySelected: _ => { },
            onStart: () => { },
            onStop: () => { },
            onRestart: () => { },
            onStopExisting: onStopExisting ?? (() => { }),
            onCheckUpdates: () => { },
            onAutoSelectStrategy: () => { },
            onCancelAutoSelect: () => { },
            onOpenSettings: () => { },
            getLastStrategyScan: () => lastScan,
            getStatusHint: statusHintProvider ?? (() => statusHint),
            onClearDiscordCache: onClearDiscordCache);
    }

    /// <summary>Visible у непоказанной формы всегда false, поэтому читаем собственный флаг видимости контрола.</summary>
    private static bool IsOwnVisible(Control control)
    {
        const int stateVisible = 0x00000002;
        var getState = typeof(Control)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(method => method.Name == "GetState" && method.GetParameters().Length == 1);
        var flagType = getState.GetParameters()[0].ParameterType;
        var flag = flagType.IsEnum ? Enum.ToObject(flagType, stateVisible) : stateVisible;
        return (bool)getState.Invoke(control, [flag])!;
    }

    private static StrategyTestSummary Summary(
        string fileName,
        int httpOk,
        int httpError,
        int youTubeOk,
        int discordOk,
        int latencyMs = 0)
    {
        return new StrategyTestSummary(
            new StrategyInfo(fileName, "C:/runtime/" + fileName, false),
            HttpOk: httpOk,
            HttpError: httpError,
            YouTubeHttpOk: youTubeOk,
            YouTubeHttpAttempts: 12,
            DiscordHttpOk: discordOk,
            DiscordHttpAttempts: 12,
            MedianLatencyMs: latencyMs);
    }

    private static IEnumerable<Control> GetAllControls(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in GetAllControls(child))
            {
                yield return nested;
            }
        }
    }
}
