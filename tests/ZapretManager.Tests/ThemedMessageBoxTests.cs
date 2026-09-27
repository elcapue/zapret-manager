using System.Reflection;
using System.Windows.Forms;
using ZapretManager.App.UI;

namespace ZapretManager.Tests;

public sealed class ThemedMessageBoxTests
{
    [Fact]
    public void Form_IsModalDialogWithoutDuplicateCaption()
    {
        using var form = CreateForm(
            "Проверить все доступные стратегии и выбрать лучшую?",
            MessageBoxButtons.YesNo);

        Assert.Equal(FormStartPosition.CenterParent, form.StartPosition);
        Assert.Equal(FormBorderStyle.FixedDialog, form.FormBorderStyle);
        Assert.False(form.ShowInTaskbar);
        Assert.DoesNotContain(form.Controls.OfType<Label>(), label => label.Text == form.Text);
        var message = Assert.Single(form.Controls.OfType<TextBox>());
        Assert.True(message.ReadOnly);
        Assert.All(
            form.Controls.OfType<Control>(),
            control => Assert.True(form.ClientRectangle.Contains(control.Bounds), $"{control.Name} выходит за границы окна"));
    }

    [Fact]
    public void Form_GrowsForLongContentButCapsHeight()
    {
        using var shortForm = CreateForm("Короткое сообщение.", MessageBoxButtons.OK);
        using var longForm = CreateForm(string.Join(' ', Enumerable.Repeat("Длинное предупреждение", 120)), MessageBoxButtons.OK);

        Assert.True(longForm.ClientSize.Height > shortForm.ClientSize.Height);
        Assert.InRange(longForm.ClientSize.Height, 140, 330);
        Assert.Equal(ScrollBars.Vertical, Assert.Single(longForm.Controls.OfType<TextBox>()).ScrollBars);
    }

    [Fact]
    public void Form_RendersBareLineFeedsAsSeparateLines()
    {
        using var form = CreateForm(
            "Доступна новая версия.\n\nТекущая версия: 1.10.2\nНовая версия: 1.10.3",
            MessageBoxButtons.YesNo);

        var message = Assert.Single(form.Controls.OfType<TextBox>());

        Assert.Equal(
            ["Доступна новая версия.", string.Empty, "Текущая версия: 1.10.2", "Новая версия: 1.10.3"],
            message.Lines);
        Assert.DoesNotMatch("(?<!\r)\n", message.Text);
    }

    [Fact]
    public void Form_AllowsExplicitActionAndCancelLabels()
    {
        using var form = CreateForm(
            "Обнаружен внешний zapret.",
            MessageBoxButtons.YesNo,
            "Остановить",
            "Отмена");

        var buttons = form.Controls.OfType<Button>().OrderBy(button => button.Left).ToArray();
        Assert.Equal(new[] { "Остановить", "Отмена" }, buttons.Select(button => button.Text));
        Assert.Equal(DialogResult.Yes, buttons[0].DialogResult);
        Assert.Equal(DialogResult.No, buttons[1].DialogResult);
    }

    private static Form CreateForm(
        string text,
        MessageBoxButtons buttons,
        string? primaryText = null,
        string? secondaryText = null)
    {
        var formType = typeof(ThemedMessageBox).Assembly.GetType(
            "ZapretManager.App.UI.ThemedMessageBoxForm",
            throwOnError: true)!;
        var args = primaryText is null && secondaryText is null
            ? new object[] { text, "Zapret Manager", buttons, MessageBoxIcon.Question }
            : new object[] { text, "Zapret Manager", buttons, MessageBoxIcon.Warning, primaryText!, secondaryText! };
        return Assert.IsAssignableFrom<Form>(Activator.CreateInstance(
            formType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args,
            culture: null));
    }
}
