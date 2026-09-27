using System.Windows.Forms;
using ZapretManager.App.Core;
using ZapretManager.App.UI;

namespace ZapretManager.Tests;

public sealed class SettingsFormTests
{
    [Fact]
    public void Constructor_ShowsOnlyProductSettingsAndUsesCommonWindowTitle()
    {
        using var form = new SettingsForm(new AppConfig
        {
            CheckForUpdatesOnStartup = true,
            StartWithWindows = true
        });
        var checkBoxes = form.Controls.OfType<CheckBox>().ToArray();

        Assert.Equal("Zapret Manager", form.Text);
        Assert.Equal(FormStartPosition.CenterParent, form.StartPosition);
        Assert.Equal(FormBorderStyle.FixedDialog, form.FormBorderStyle);
        Assert.False(form.ShowInTaskbar);
        Assert.NotNull(form.Icon);
        Assert.Equal(2, checkBoxes.Length);
        Assert.Contains(checkBoxes, checkBox =>
            checkBox.Text == "Проверять обновления при запуске" && checkBox.Checked);
        Assert.Contains(checkBoxes, checkBox =>
            checkBox.Text == "Запускать zapret вместе с Windows" && checkBox.Checked);
        Assert.Contains(
            form.Controls.OfType<Label>(),
            label => label.Text == "Менеджер запустится в фоне и включит последнюю выбранную стратегию.");
        Assert.Contains(
            form.Controls.OfType<Label>(),
            label => label.Name == "CheckUpdatesOnStartupDescription" && label.Text.Contains("уведомление"));
        Assert.True(form.StartWithWindows);
        var closeButton = Assert.Single(form.Controls.OfType<Button>());
        Assert.Equal("Закрыть", closeButton.Text);
        Assert.Same(closeButton, form.CancelButton);
        Assert.All(
            form.Controls.OfType<Control>(),
            control => Assert.True(form.ClientRectangle.Contains(control.Bounds), $"{control.Name} выходит за границы окна"));
    }

    [Fact]
    public void CheckBoxes_ApplyImmediately()
    {
        bool? checkUpdates = null;
        bool? startWithWindows = null;
        using var form = new SettingsForm(
            new AppConfig(),
            enabled => checkUpdates = enabled,
            enabled =>
            {
                startWithWindows = enabled;
                return true;
            });

        GetCheckBox(form, "Проверять обновления при запуске").Checked = true;
        GetCheckBox(form, "Запускать zapret вместе с Windows").Checked = true;

        Assert.True(checkUpdates);
        Assert.True(startWithWindows);
        Assert.True(form.StartWithWindows);
    }

    [Fact]
    public void StartWithWindows_WhenApplyFails_RevertsCheckBox()
    {
        var calls = 0;
        using var form = new SettingsForm(
            new AppConfig(),
            onStartWithWindowsChanged: _ =>
            {
                calls++;
                return false;
            });

        GetCheckBox(form, "Запускать zapret вместе с Windows").Checked = true;

        Assert.False(form.StartWithWindows);
        Assert.Equal(1, calls);
    }

    private static CheckBox GetCheckBox(Form form, string text)
    {
        return form.Controls.OfType<CheckBox>().Single(checkBox => checkBox.Text == text);
    }
}
