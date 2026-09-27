using ZapretManager.App.Core;

namespace ZapretManager.App.UI;

public sealed record TrayMenuActions(
    Action OnOpenMainWindow,
    Action OnStart,
    Action OnStop,
    Action OnRestart,
    Action OnStopExisting,
    Action<StrategyInfo> OnStrategySelected,
    Action OnAutoSelectStrategy,
    Action OnClearDiscordCache,
    Action OnCheckUpdates,
    Action OnOpenSettings,
    Action OnExit);

public static class TrayMenuFactory
{
    public static ContextMenuStrip Create(ZapretState state, IReadOnlyList<StrategyInfo> strategies, TrayMenuActions actions)
    {
        var menu = new ContextMenuStrip();
        Populate(menu, state, strategies, actions);
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
        TrayMenuActions actions)
    {
        var previousItems = menu.Items.Cast<ToolStripItem>().ToArray();
        menu.Items.Clear();
        foreach (var item in previousItems)
        {
            item.Dispose();
        }

        menu.Items.Add(new ToolStripMenuItem("Статус: " + state.ToDisplayText().ToLowerInvariant()) { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add(Item("Открыть", actions.OnOpenMainWindow));

        var zapretItems = CreateZapretItems(state, actions);
        if (zapretItems.Length > 0)
        {
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.AddRange(zapretItems);
        }

        menu.Items.Add(new ToolStripSeparator());
        var strategyMenu = new ToolStripMenuItem("Стратегия");
        AddStrategyItems(strategyMenu, strategies, actions);
        menu.Items.Add(strategyMenu);
        menu.Items.Add(Item("Очистить кеш Discord...", actions.OnClearDiscordCache));
        menu.Items.Add(Item("Проверить обновления", actions.OnCheckUpdates));
        menu.Items.Add(Item("Настройки", actions.OnOpenSettings));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Item("Выход", actions.OnExit));

        UiTheme.StyleMenu(menu);
    }

    private static ToolStripItem[] CreateZapretItems(ZapretState state, TrayMenuActions actions)
    {
        return state switch
        {
            ZapretState.Running => [Item("Выключить", actions.OnStop), Item("Перезапустить", actions.OnRestart)],
            ZapretState.Stopped => [Item("Включить", actions.OnStart)],
            ZapretState.External => [Item("Внешний zapret...", actions.OnStopExisting)],
            _ => []
        };
    }

    private static void AddStrategyItems(
        ToolStripMenuItem strategyMenu,
        IReadOnlyList<StrategyInfo> strategies,
        TrayMenuActions actions)
    {
        strategyMenu.DropDownItems.Add(Item("Автовыбор...", actions.OnAutoSelectStrategy));
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
            strategyItem.Click += (_, _) => actions.OnStrategySelected(strategy);
            strategyMenu.DropDownItems.Add(strategyItem);
        }
    }

    private static ToolStripMenuItem Item(string text, Action onClick)
    {
        return new ToolStripMenuItem(text, image: null, onClick: (_, _) => onClick());
    }
}
