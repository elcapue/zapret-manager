using System.Drawing;
using System.Windows.Forms;
using ZapretManager.App.UI;

namespace ZapretManager.Tests;

public sealed class ToastNotificationTests
{
    [Theory]
    [InlineData(5, false)] // QUNS_ACCEPTS_NOTIFICATIONS
    [InlineData(1, true)] // экран заблокирован / хранитель экрана
    [InlineData(2, true)] // полноэкранное приложение
    [InlineData(3, true)] // Direct3D на весь экран (игра)
    [InlineData(4, true)] // режим презентации
    [InlineData(6, true)] // «тихие часы»
    public void ShouldDeferToSystem_ShowsOwnToastOnlyWhenWindowsAcceptsNotifications(int state, bool expected)
    {
        Assert.Equal(expected, ToastWindow.ShouldDeferToSystem(state));
    }

    [Fact]
    public void Constructor_GrowsWithMessageAndKeepsFixedWidth()
    {
        using var shortToast = new ToastWindow("Готово.");
        using var longToast = new ToastWindow(string.Join(" ", Enumerable.Repeat("длинное сообщение", 20)));

        Assert.Equal(ToastWindow.ToastWidth, shortToast.ClientSize.Width);
        Assert.Equal(ToastWindow.ToastWidth, longToast.ClientSize.Width);
        Assert.True(longToast.ClientSize.Height > shortToast.ClientSize.Height);
        Assert.False(shortToast.ShowInTaskbar);
        Assert.Equal(FormBorderStyle.None, shortToast.FormBorderStyle);
    }

    [Fact]
    public void Click_RunsNotificationActionButCloseButtonDoesNot()
    {
        var clicks = 0;
        using var toast = new ToastWindow("zapret неожиданно остановился.", () => clicks++);
        using var other = new ToastWindow("zapret неожиданно остановился.", () => clicks++);

        toast.PerformToastClick(new Point(ToastWindow.ToastWidth - 20, 16));
        other.PerformToastClick(new Point(80, 40));

        Assert.True(toast.IsClickable);
        Assert.Equal(1, clicks);
    }

    [Fact]
    public void ShowInformation_WhenUserIsBusy_UsesSystemNotificationInsteadOfOwnWindow()
    {
        using var icon = new NotifyIcon();
        using var notifications = new TrayNotificationService(icon, shouldDeferToSystem: () => true);

        notifications.ShowInformation("Настройки применены.");

        Assert.Null(notifications.CurrentToast);
    }

    [Fact]
    public void Normalize_KeepsLineBreaksButDropsEmptyLines()
    {
        var text = TrayNotificationService.Normalize("  Первая строка \n\n  Вторая  \r\n");

        Assert.Equal("Первая строка" + Environment.NewLine + "Вторая", text);
    }
}
