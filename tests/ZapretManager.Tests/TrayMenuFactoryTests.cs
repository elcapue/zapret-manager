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
        using var menu = TrayMenuFactory.Create(state, [], new FakeZapretManagerCommands());

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
        var commands = new FakeZapretManagerCommands();
        using var menu = TrayMenuFactory.Create(ZapretState.Stopped, [], commands);

        TrayMenuFactory.Populate(menu, ZapretState.Running, [], commands);

        var labels = menu.Items.OfType<ToolStripItem>().Select(item => item.Text).ToArray();
        Assert.Contains("Выключить", labels);
        Assert.DoesNotContain("Включить", labels);
        Assert.Single(labels, label => label == "Выход");
    }

    [Fact]
    public void Create_ExternalItemUsesStopExistingFlow()
    {
        var commands = new FakeZapretManagerCommands();
        using var menu = TrayMenuFactory.Create(ZapretState.External, [], commands);

        var item = Assert.IsType<ToolStripMenuItem>(menu.Items
            .OfType<ToolStripItem>()
            .Single(candidate => candidate.Text == "Внешний zapret..."));
        item.PerformClick();

        Assert.Equal(["StopExistingZapret"], commands.Calls);
    }

    [Fact]
    public void Create_WhenStrategiesProvided_AddsStrategiesAndChecksSelectedItem()
    {
        var strategies = new[]
        {
            new StrategyInfo("general.bat", "C:/runtime/general.bat", isSelected: false),
            new StrategyInfo("general (ALT2).bat", "C:/runtime/general (ALT2).bat", isSelected: true)
        };
        var commands = new FakeZapretManagerCommands();

        using var menu = TrayMenuFactory.Create(ZapretState.Stopped, strategies, commands);

        var strategyMenu = GetStrategyMenu(menu);
        var strategyItems = strategyMenu.DropDownItems.OfType<ToolStripMenuItem>().Skip(1).ToArray();

        Assert.Equal(new[] { "general", "general (ALT2)" }, strategyItems.Select(item => item.Text));
        Assert.False(strategyItems[0].Checked);
        Assert.True(strategyItems[1].Checked);

        strategyItems[0].PerformClick();

        Assert.Equal(["SelectStrategy:general.bat"], commands.Calls);
    }

    [Fact]
    public void Create_StrategyMenuStartsWithAutoSelect()
    {
        var commands = new FakeZapretManagerCommands();

        using var menu = TrayMenuFactory.Create(ZapretState.Stopped, [], commands);

        var autoSelectItem = Assert.IsType<ToolStripMenuItem>(GetStrategyMenu(menu).DropDownItems[0]);
        Assert.Equal("Автовыбор...", autoSelectItem.Text);

        autoSelectItem.PerformClick();

        Assert.Equal(["RunStrategyAutoSelection"], commands.Calls);
    }

    [Fact]
    public void Create_ClearDiscordCacheItemInvokesHandler()
    {
        var commands = new FakeZapretManagerCommands();

        using var menu = TrayMenuFactory.Create(ZapretState.Stopped, [], commands);

        var item = Assert.IsType<ToolStripMenuItem>(menu.Items
            .OfType<ToolStripItem>()
            .Single(candidate => candidate.Text == "Очистить кеш Discord..."));
        item.PerformClick();

        Assert.Equal(["ClearDiscordCache"], commands.Calls);
    }

    private static ToolStripMenuItem GetStrategyMenu(ContextMenuStrip menu)
    {
        return Assert.IsType<ToolStripMenuItem>(menu.Items
            .OfType<ToolStripItem>()
            .Single(item => item.Text == "Стратегия"));
    }
}
