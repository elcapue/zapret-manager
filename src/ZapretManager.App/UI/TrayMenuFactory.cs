using ZapretManager.App.Core;

namespace ZapretManager.App.UI;

public static class TrayMenuFactory
{
    public static ContextMenuStrip Create(ZapretState state, IReadOnlyList<StrategyInfo> strategies, IZapretManagerCommands commands)
    {
        var menu = new ContextMenuStrip();
        Populate(menu, state, strategies, commands);
        return menu;
    }

    /// <summary>
    /// Заполняет меню под текущее состояние. Вызывается при каждом открытии меню,
    /// поэтому статус и доступные действия всегда актуальны.
    /// </summary>
    public static void Populate(
        ContextMenuStrip menu,
        ZapretState state,
        IReadOnlyList<StrategyInfo> strategies,
        IZapretManagerCommands commands)
    {
        var previousItems = menu.Items.Cast<ToolStripItem>().ToArray();
        menu.Items.Clear();
        foreach (var item in previousItems)
        {
            item.Dispose();
        }

        menu.Items.Add(new ToolStripMenuItem("Статус: " + state.ToDisplayText().ToLowerInvariant()) { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add(Item("Открыть", commands.ShowMainWindow));

        var zapretItems = CreateZapretItems(state, commands);
        if (zapretItems.Length > 0)
        {
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.AddRange(zapretItems);
        }

        menu.Items.Add(new ToolStripSeparator());
        var strategyMenu = new ToolStripMenuItem("Стратегия");
        AddStrategyItems(strategyMenu, strategies, commands);
        menu.Items.Add(strategyMenu);
        menu.Items.Add(Item("Очистить кеш Discord...", commands.ClearDiscordCache));
        menu.Items.Add(Item("Проверить обновления", commands.CheckUpdates));
        menu.Items.Add(Item("Настройки", commands.OpenSettings));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Item("Выход", commands.ExitApplication));

        UiTheme.StyleMenu(menu);
    }

    private static ToolStripItem[] CreateZapretItems(ZapretState state, IZapretManagerCommands commands)
    {
        return state switch
        {
            ZapretState.Running => [Item("Выключить", commands.StopZapret), Item("Перезапустить", commands.RestartZapret)],
            ZapretState.Stopped => [Item("Включить", commands.StartZapret)],
            ZapretState.External => [Item("Внешний zapret...", commands.StopExistingZapret)],
            _ => []
        };
    }

    private static void AddStrategyItems(
        ToolStripMenuItem strategyMenu,
        IReadOnlyList<StrategyInfo> strategies,
        IZapretManagerCommands commands)
    {
        strategyMenu.DropDownItems.Add(Item("Автовыбор...", commands.RunStrategyAutoSelection));
        strategyMenu.DropDownItems.Add(new ToolStripSeparator());

        if (strategies.Count == 0)
        {
            strategyMenu.DropDownItems.Add(new ToolStripMenuItem("Пока нет runtime") { Enabled = false });
            return;
        }

        foreach (var strategy in strategies)
        {
            var strategyItem = new ToolStripMenuItem(strategy.DisplayName)
            {
                Checked = strategy.IsSelected
            };
            strategyItem.Click += (_, _) => commands.SelectStrategy(strategy);
            strategyMenu.DropDownItems.Add(strategyItem);
        }
    }

    private static ToolStripMenuItem Item(string text, Action onClick)
    {
        return new ToolStripMenuItem(text, image: null, onClick: (_, _) => onClick());
    }
}
