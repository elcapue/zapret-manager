namespace ZapretManager.App.UI;

/// <summary>
/// Уведомления менеджера. Обычно — собственное окно в стиле приложения (<see cref="ToastWindow"/>);
/// когда пользователь занят (игра на весь экран, презентация, «Не беспокоить», заблокированный экран),
/// уведомление уходит в системный механизм Windows, который учитывает эти режимы.
/// </summary>
public sealed class TrayNotificationService : IDisposable
{
    private const int BalloonTimeoutMilliseconds = 5_000;
    private const int MaximumMessageLength = 240;

    private readonly NotifyIcon _notifyIcon;
    private readonly Func<bool> _shouldDeferToSystem;
    private ToastWindow? _currentToast;
    private Action? _balloonClickAction;

    public TrayNotificationService(NotifyIcon notifyIcon, Func<bool>? shouldDeferToSystem = null)
    {
        _notifyIcon = notifyIcon;
        _shouldDeferToSystem = shouldDeferToSystem ?? ToastWindow.ShouldDeferToSystem;
        _notifyIcon.BalloonTipClicked += (_, _) =>
        {
            var action = _balloonClickAction;
            _balloonClickAction = null;
            action?.Invoke();
        };
    }

    internal ToastWindow? CurrentToast => _currentToast;

    /// <summary>
    /// Показывает уведомление. <paramref name="onClick"/> относится только к этому уведомлению:
    /// следующее заменяет и само уведомление, и его действие.
    /// </summary>
    public void ShowInformation(string message, Action? onClick = null)
    {
        var text = Normalize(message);
        CloseCurrentToast();
        _balloonClickAction = null;

        if (_shouldDeferToSystem())
        {
            _balloonClickAction = onClick;
            _notifyIcon.ShowBalloonTip(BalloonTimeoutMilliseconds, "Zapret Manager", text, ToolTipIcon.Info);
            return;
        }

        var toast = new ToastWindow(text, onClick);
        // Немодальную форму WinForms освобождает сама после закрытия.
        toast.FormClosed += (_, _) =>
        {
            if (ReferenceEquals(_currentToast, toast))
            {
                _currentToast = null;
            }
        };
        _currentToast = toast;
        toast.ShowToast();
    }

    public void Dispose()
    {
        CloseCurrentToast();
    }

    private void CloseCurrentToast()
    {
        var toast = _currentToast;
        _currentToast = null;
        toast?.CloseNow();
    }

    internal static string Normalize(string message)
    {
        // Переносы строк сохраняем (окно уведомления их показывает), но убираем пустые и лишние пробелы.
        var text = string.Join(Environment.NewLine, message
            .Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return text.Length <= MaximumMessageLength
            ? text
            : text[..(MaximumMessageLength - 1)] + "…";
    }
}
