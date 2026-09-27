using ZapretManager.App.Core;

namespace ZapretManager.App.UI;

/// <summary>
/// Настройки применяются сразу по клику: отдельного «Сохранить» нет.
/// Если изменение не удалось (например, Планировщик отказал), галочка возвращается назад.
/// </summary>
public sealed class SettingsForm : Form
{
    /// <summary>Выше стандартного диалога: у каждой настройки есть строка пояснения.</summary>
    internal const int FormHeight = 260;

    private readonly CheckBox _checkUpdatesOnStartupCheckBox;
    private readonly CheckBox _startWithWindowsCheckBox;
    private bool _reverting;

    public SettingsForm(
        AppConfig config,
        Action<bool>? onCheckUpdatesOnStartupChanged = null,
        Func<bool, bool>? onStartWithWindowsChanged = null)
    {
        Text = "Zapret Manager";
        ClientSize = new Size(UiTheme.DialogWidth, FormHeight);
        UiTheme.ApplyDialogWindow(this);

        var titleLabel = new Label
        {
            Text = "Настройки",
            AutoSize = false,
            Location = new Point(24, 18),
            Size = new Size(280, 30)
        };
        UiTheme.StyleLabel(titleLabel, UiTheme.Text, 14F, FontStyle.Bold);

        _checkUpdatesOnStartupCheckBox = new CheckBox
        {
            Text = "Проверять обновления при запуске",
            AutoSize = true,
            Checked = config.CheckForUpdatesOnStartup,
            Location = new Point(26, 62)
        };
        UiTheme.StyleCheckBox(_checkUpdatesOnStartupCheckBox);
        _checkUpdatesOnStartupCheckBox.CheckedChanged += (_, _) =>
            onCheckUpdatesOnStartupChanged?.Invoke(_checkUpdatesOnStartupCheckBox.Checked);

        var checkUpdatesDescription = CreateDescription(
            "CheckUpdatesOnStartupDescription",
            "Если вышла новая версия Zapret Manager или zapret (Flowseal), придёт уведомление.",
            90);

        _startWithWindowsCheckBox = new CheckBox
        {
            Name = "StartWithWindowsCheckBox",
            Text = "Запускать zapret вместе с Windows",
            AutoSize = true,
            Checked = config.StartWithWindows,
            Location = new Point(26, 130)
        };
        UiTheme.StyleCheckBox(_startWithWindowsCheckBox);
        _startWithWindowsCheckBox.CheckedChanged += (_, _) =>
        {
            if (_reverting || onStartWithWindowsChanged is null)
            {
                return;
            }

            var requested = _startWithWindowsCheckBox.Checked;
            if (!onStartWithWindowsChanged(requested))
            {
                _reverting = true;
                _startWithWindowsCheckBox.Checked = !requested;
                _reverting = false;
            }
        };

        var startWithWindowsDescription = CreateDescription(
            "StartWithWindowsDescription",
            "Менеджер запустится в фоне и включит последнюю выбранную стратегию.",
            158);

        var closeButton = new ThemedButton
        {
            Text = "Закрыть",
            DialogResult = DialogResult.OK,
            Location = new Point(
                UiTheme.DialogWidth - UiTheme.DialogPadding - 92,
                FormHeight - UiTheme.DialogPadding - UiTheme.DialogButtonHeight),
            Size = new Size(92, UiTheme.DialogButtonHeight),
            Cursor = Cursors.Hand
        };
        UiTheme.StyleButton(closeButton);

        Controls.Add(titleLabel);
        Controls.Add(_checkUpdatesOnStartupCheckBox);
        Controls.Add(checkUpdatesDescription);
        Controls.Add(_startWithWindowsCheckBox);
        Controls.Add(startWithWindowsDescription);
        Controls.Add(closeButton);
        AcceptButton = closeButton;
        CancelButton = closeButton;
    }

    public bool CheckForUpdatesOnStartup => _checkUpdatesOnStartupCheckBox.Checked;

    public bool StartWithWindows => _startWithWindowsCheckBox.Checked;

    /// <summary>Пояснение под галочкой: отступ совпадает с текстом галочки.</summary>
    private static Label CreateDescription(string name, string text, int top)
    {
        var label = new Label
        {
            Name = name,
            Text = text,
            AutoSize = false,
            Location = new Point(46, top),
            Size = new Size(450, 34)
        };
        UiTheme.StyleLabel(label, UiTheme.MutedText, 8.5F);
        return label;
    }
}
