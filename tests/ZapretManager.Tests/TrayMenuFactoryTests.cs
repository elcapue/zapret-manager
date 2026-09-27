using System.Windows.Forms;
using ZapretManager.App.Core;
using ZapretManager.App.UI;

namespace ZapretManager.Tests;

public sealed class TrayMenuFactoryTests
{
    [Theory]
    [InlineData(ZapretState.Running, "Статус: включено", new[] { "Выключить", "Перезапустить" })]
    [InlineData(ZapretState.Stopped, "Статус: выключено", new[] { "Включить" })]
    [InlineData(ZapretState.External, "Статус: внешний zapret", new[] { "Внешний zapret..." })]
    [InlineData(ZapretState.RuntimeMissing, "Статус: нет runtime", new string[0])]
    public void Create_ShowsOnlyActionsThatMatchState(ZapretState state, string statusText, string[] zapretActions)
    {
        using var menu = TrayMenuFactory.Create(state, [], CreateActions());

        var labels = menu.Items
            .OfType<ToolStripItem>()
            .Where(item => item is not ToolStripSeparator)
            .Select(item => item.Text)
            .ToArray();

        string[] expected =
        [
            statusText,
            "Открыть",
            .. zapretActions,
            "Стратегия",
            "Очистить кеш Discord...",
            "Проверить обновления",
            "Настройки",
            "Выход"
        ];
        Assert.Equal(expected, labels);
        Assert.False(menu.Items[0].Enabled);
    }

    [Fact]
    public void Populate_ReplacesItemsWhenStateChanges()
    {
        var actions = CreateActions();
        using var menu = TrayMenuFactory.Create(ZapretState.Stopped, [], actions);

        TrayMenuFactory.Populate(menu, ZapretState.Running, [], actions);

        var labels = menu.Items.OfType<ToolStripItem>().Select(item => item.Text).ToArray();
        Assert.Contains("Выключить", labels);
        Assert.DoesNotContain("Включить", labels);
        Assert.Single(labels, label => label == "Выход");
    }

    [Fact]
    public void Create_ExternalItemUsesStopExistingFlow()
    {
        var clicked = false;
        using var menu = TrayMenuFactory.Create(
            ZapretState.External,
            [],
            CreateActions(onStopExisting: () => clicked = true));

        var item = Assert.IsType<ToolStripMenuItem>(menu.Items
            .OfType<ToolStripItem>()
            .Single(candidate => candidate.Text == "Внешний zapret..."));
        item.PerformClick();

        Assert.True(clicked);
    }

    [Fact]
    public void Create_WhenStrategiesProvided_AddsStrategiesAndChecksSelectedItem()
    {
        var strategies = new[]
        {
            new StrategyInfo("general.bat", "C:/runtime/general.bat", isSelected: false),
            new StrategyInfo("general (ALT2).bat", "C:/runtime/general (ALT2).bat", isSelected: true)
        };
        StrategyInfo? selected = null;

        using var menu = TrayMenuFactory.Create(
            ZapretState.Stopped,
            strategies,
            CreateActions(onStrategySelected: strategy => selected = strategy));

        var strategyMenu = GetStrategyMenu(menu);
        var strategyItems = strategyMenu.DropDownItems.OfType<ToolStripMenuItem>().Skip(1).ToArray();

        Assert.Equal(new[] { "general", "general (ALT2)" }, strategyItems.Select(item => item.Text));
        Assert.False(strategyItems[0].Checked);
        Assert.True(strategyItems[1].Checked);

        strategyItems[0].PerformClick();

        Assert.Equal("general.bat", selected?.FileName);
    }

    [Fact]
    public void Create_StrategyMenuStartsWithAutoSelect()
    {
        var clicked = false;

        using var menu = TrayMenuFactory.Create(
            ZapretState.Stopped,
            [],
            CreateActions(onAutoSelect: () => clicked = true));

        var autoSelectItem = Assert.IsType<ToolStripMenuItem>(GetStrategyMenu(menu).DropDownItems[0]);
        Assert.Equal("Автовыбор...", autoSelectItem.Text);

        autoSelectItem.PerformClick();

        Assert.True(clicked);
    }

    [Fact]
    public void Create_ClearDiscordCacheItemInvokesHandler()
    {
        var clicked = false;

        using var menu = TrayMenuFactory.Create(
            ZapretState.Stopped,
            [],
            CreateActions(onClearDiscordCache: () => clicked = true));

        var item = Assert.IsType<ToolStripMenuItem>(menu.Items
            .OfType<ToolStripItem>()
            .Single(candidate => candidate.Text == "Очистить кеш Discord..."));
        item.PerformClick();

        Assert.True(clicked);
    }

    private static ToolStripMenuItem GetStrategyMenu(ContextMenuStrip menu)
    {
        return Assert.IsType<ToolStripMenuItem>(menu.Items
            .OfType<ToolStripItem>()
            .Single(item => item.Text == "Стратегия"));
    }

    private static TrayMenuActions CreateActions(
        Action? onStopExisting = null,
        Action<StrategyInfo>? onStrategySelected = null,
        Action? onAutoSelect = null,
        Action? onClearDiscordCache = null)
    {
        return new TrayMenuActions(
            OnOpenMainWindow: () => { },
            OnStart: () => { },
            OnStop: () => { },
            OnRestart: () => { },
            OnStopExisting: onStopExisting ?? (() => { }),
            OnStrategySelected: onStrategySelected ?? (_ => { }),
            OnAutoSelectStrategy: onAutoSelect ?? (() => { }),
            OnClearDiscordCache: onClearDiscordCache ?? (() => { }),
            OnCheckUpdates: () => { },
            OnOpenSettings: () => { },
            OnExit: () => { });
    }
}
