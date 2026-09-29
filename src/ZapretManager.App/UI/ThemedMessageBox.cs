namespace ZapretManager.App.UI;

public static class ThemedMessageBox
{
    public static DialogResult Show(string text, string caption)
    {
        return Show(text, caption, MessageBoxButtons.OK, MessageBoxIcon.None);
    }

    public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
    {
        using var dialog = new ThemedMessageBoxForm(text, caption, buttons, icon);
        return ShowDialog(dialog);
    }

    public static DialogResult ShowAction(
        string text,
        string caption,
        string actionText,
        string cancelText,
        MessageBoxIcon icon = MessageBoxIcon.Warning)
    {
        using var dialog = new ThemedMessageBoxForm(
            text,
            caption,
            MessageBoxButtons.YesNo,
            icon,
            actionText,
            cancelText);
        return ShowDialog(dialog);
    }

    private static DialogResult ShowDialog(Form dialog)
    {
        var owner = Form.ActiveForm;
        return owner is not null && !owner.IsDisposed
            ? dialog.ShowDialog(owner)
            : dialog.ShowDialog();
    }
}

internal sealed class ThemedMessageBoxForm : Form
{
    private const int MessageWidth = UiTheme.DialogWidth - UiTheme.DialogPadding * 2;

    public ThemedMessageBoxForm(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        : this(text, caption, buttons, icon, null, null)
    {
    }

    internal ThemedMessageBoxForm(
        string text,
        string caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon,
        string? primaryText,
        string? secondaryText)
    {
        Text = caption;
        UiTheme.ApplyDialogWindow(this);

        // Многострочный TextBox переносит строку только по \r\n, а сообщения по всему приложению
        // собираются с \n — без нормализации текст склеивается в одну строку.
        text = text.ReplaceLineEndings("\r\n");
        var messageFont = UiTheme.GetFont(9F);
        var measured = TextRenderer.MeasureText(
            text,
            messageFont,
            new Size(MessageWidth, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
        var messageHeight = Math.Clamp(measured.Height + 8, 42, 220);

        var messageBox = new TextBox
        {
            Text = text,
            ReadOnly = true,
            Multiline = true,
            BorderStyle = BorderStyle.None,
            BackColor = UiTheme.WindowBack,
            ForeColor = UiTheme.Text,
            Font = messageFont,
            Location = new Point(UiTheme.DialogPadding, UiTheme.DialogPadding),
            Size = new Size(MessageWidth, messageHeight),
            ScrollBars = measured.Height > messageHeight ? ScrollBars.Vertical : ScrollBars.None,
            TabStop = false
        };

        var buttonSpecs = BuildButtonSpecs(buttons, primaryText, secondaryText);
        var buttonTop = messageBox.Bottom + 20;
        var dialogHeight = buttonTop + UiTheme.DialogButtonHeight + UiTheme.DialogPadding;
        ClientSize = new Size(UiTheme.DialogWidth, dialogHeight);

        Controls.Add(messageBox);
        AddButtons(buttonSpecs, buttonTop);
    }

    private void AddButtons(IReadOnlyList<ButtonSpec> buttonSpecs, int buttonTop)
    {
        var buttonWidths = buttonSpecs
            .Select(button => Math.Max(82, TextRenderer.MeasureText(button.Text, Font).Width + 30))
            .ToArray();
        var totalWidth = buttonWidths.Sum() + UiTheme.DialogButtonGap * (buttonWidths.Length - 1);
        var left = ClientSize.Width - UiTheme.DialogPadding - totalWidth;

        for (var index = 0; index < buttonSpecs.Count; index++)
        {
            var spec = buttonSpecs[index];
            var button = new ThemedButton
            {
                Text = spec.Text,
                DialogResult = spec.Result,
                Location = new Point(left, buttonTop),
                Size = new Size(buttonWidths[index], UiTheme.DialogButtonHeight),
                Cursor = Cursors.Hand
            };
            UiTheme.StyleButton(button, spec.Kind);
            button.Click += (_, _) =>
            {
                DialogResult = spec.Result;
                Close();
            };

            Controls.Add(button);

            if (index == 0)
            {
                AcceptButton = button;
            }

            if (spec.Result is DialogResult.Cancel or DialogResult.No)
            {
                CancelButton = button;
            }

            left += buttonWidths[index] + UiTheme.DialogButtonGap;
        }
    }

    private static IReadOnlyList<ButtonSpec> BuildButtonSpecs(
        MessageBoxButtons buttons,
        string? primaryText,
        string? secondaryText)
    {
        return buttons switch
        {
            MessageBoxButtons.OKCancel => new[]
            {
                new ButtonSpec(primaryText ?? "ОК", DialogResult.OK, UiButtonKind.Primary),
                new ButtonSpec(secondaryText ?? "Отмена", DialogResult.Cancel, UiButtonKind.Secondary)
            },
            MessageBoxButtons.YesNo => new[]
            {
                new ButtonSpec(primaryText ?? "Да", DialogResult.Yes, UiButtonKind.Primary),
                new ButtonSpec(secondaryText ?? "Нет", DialogResult.No, UiButtonKind.Secondary)
            },
            MessageBoxButtons.YesNoCancel => new[]
            {
                new ButtonSpec("Да", DialogResult.Yes, UiButtonKind.Primary),
                new ButtonSpec("Нет", DialogResult.No, UiButtonKind.Secondary),
                new ButtonSpec("Отмена", DialogResult.Cancel, UiButtonKind.Secondary)
            },
            _ => new[]
            {
                new ButtonSpec("ОК", DialogResult.OK, UiButtonKind.Primary)
            }
        };
    }

    private sealed record ButtonSpec(string Text, DialogResult Result, UiButtonKind Kind);
}
