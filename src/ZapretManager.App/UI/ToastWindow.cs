using System.Runtime.InteropServices;

namespace ZapretManager.App.UI;

/// <summary>
/// Уведомление в стиле приложения: тёмная карточка в правом нижнем углу рабочей области.
/// Не забирает фокус, исчезает сама (пока курсор над ней — ждёт), клик выполняет действие уведомления.
/// Всё рисуется в OnPaint без дочерних контролов, чтобы наведение и клик обрабатывались в одном месте.
/// </summary>
public sealed class ToastWindow : Form
{
    internal const int ToastWidth = 360;
    internal const int VisibleMilliseconds = 6_000;
    private const int Inset = 14;
    private const int AccentWidth = 3;
    private const int IconSize = 28;
    private const int TextLeft = Inset + AccentWidth + IconSize + 12;
    private const int CloseSize = 20;
    private const int ScreenMargin = 12;
    private const int FadeStepMilliseconds = 15;
    private const double FadeStep = 0.12;

    private const int WsExTopmost = 0x00000008;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private readonly string _message;
    private readonly Action? _onClick;
    private readonly Font _titleFont = new("Segoe UI Semibold", 9F, FontStyle.Bold, GraphicsUnit.Point);
    private readonly Font _messageFont = new("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
    private readonly Bitmap? _logo;
    private readonly System.Windows.Forms.Timer _lifetimeTimer = new() { Interval = VisibleMilliseconds };
    private readonly System.Windows.Forms.Timer _fadeTimer = new() { Interval = FadeStepMilliseconds };
    private readonly Rectangle _messageBounds;
    private bool _fadingOut;
    private bool _isHovered;
    private bool _isCloseHovered;

    public ToastWindow(string message, Action? onClick = null)
    {
        _message = message;
        _onClick = onClick;
        _logo = AppAssets.LoadMark();

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        BackColor = UiTheme.Surface;
        ForeColor = UiTheme.Text;
        Cursor = onClick is null ? Cursors.Default : Cursors.Hand;
        AccessibleRole = AccessibleRole.Alert;
        AccessibleName = "Zapret Manager: " + message;
        DoubleBuffered = true;

        var messageWidth = ToastWidth - TextLeft - Inset;
        var messageHeight = TextRenderer.MeasureText(
            message,
            _messageFont,
            new Size(messageWidth, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
        _messageBounds = new Rectangle(TextLeft, 34, messageWidth, messageHeight);
        ClientSize = new Size(ToastWidth, Math.Max(IconSize + Inset * 2, _messageBounds.Bottom + Inset));

        _lifetimeTimer.Tick += (_, _) => BeginFadeOut();
        _fadeTimer.Tick += (_, _) => StepFade();
    }

    public bool IsClickable => _onClick is not null;

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= WsExTopmost | WsExToolWindow | WsExNoActivate;
            return parameters;
        }
    }

    public void ShowToast()
    {
        var area = Screen.PrimaryScreen?.WorkingArea ?? SystemInformation.WorkingArea;
        Location = new Point(area.Right - Width - ScreenMargin, area.Bottom - Height - ScreenMargin);
        Opacity = 0;
        Show();
        _fadeTimer.Start();
        _lifetimeTimer.Start();
    }

    /// <summary>Закрывает сразу, без анимации — когда его сменяет новое уведомление.</summary>
    public void CloseNow()
    {
        _lifetimeTimer.Stop();
        _fadeTimer.Stop();
        Close();
    }

    /// <summary>
    /// В полноэкранной игре, презентации или режиме «Не беспокоить» своё окно выскочило бы поверх —
    /// тогда уведомление отдаётся Windows, которая сама решает, показывать ли его.
    /// </summary>
    public static bool ShouldDeferToSystem()
    {
        try
        {
            return SHQueryUserNotificationState(out var state) == 0 && ShouldDeferToSystem(state);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Своё окно — только когда Windows сама готова показывать уведомления (QUNS_ACCEPTS_NOTIFICATIONS).
    /// Экран заблокирован, игра на весь экран, презентация, «Не беспокоить» — решает Windows.
    /// </summary>
    internal static bool ShouldDeferToSystem(int notificationState)
    {
        const int acceptsNotifications = 5;
        return notificationState != acceptsNotifications;
    }

    internal void PerformToastClick(Point location)
    {
        if (GetCloseBounds().Contains(location))
        {
            BeginFadeOut();
            return;
        }

        var action = _onClick;
        CloseNow();
        action?.Invoke();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        UiDrawing.ConfigureSurfaceGraphics(e.Graphics);
        e.Graphics.Clear(_isHovered && IsClickable ? UiTheme.SurfaceRaised : UiTheme.Surface);

        using (var accent = new SolidBrush(UiTheme.Accent))
        {
            e.Graphics.FillRectangle(accent, 0, 0, AccentWidth, Height);
        }

        using (var border = new Pen(UiTheme.Border))
        {
            e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        }

        if (_logo is not null)
        {
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            e.Graphics.DrawImage(_logo, new Rectangle(Inset + AccentWidth, Inset, IconSize, IconSize));
        }

        TextRenderer.DrawText(
            e.Graphics,
            "Zapret Manager",
            _titleFont,
            new Rectangle(TextLeft, 12, _messageBounds.Width - CloseSize, 18),
            UiTheme.Text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        TextRenderer.DrawText(
            e.Graphics,
            _message,
            _messageFont,
            _messageBounds,
            UiTheme.StatusNeutral,
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);

        var close = GetCloseBounds();
        using var closePen = new Pen(_isCloseHovered ? UiTheme.Text : UiTheme.MutedText, 1.5F);
        const int crossInset = 6;
        e.Graphics.DrawLine(closePen, close.Left + crossInset, close.Top + crossInset, close.Right - crossInset, close.Bottom - crossInset);
        e.Graphics.DrawLine(closePen, close.Right - crossInset, close.Top + crossInset, close.Left + crossInset, close.Bottom - crossInset);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        // Пока пользователь читает, уведомление не исчезает.
        _isHovered = true;
        _lifetimeTimer.Stop();
        if (_fadingOut)
        {
            _fadingOut = false;
            _fadeTimer.Start();
        }

        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _isHovered = false;
        _isCloseHovered = false;
        _lifetimeTimer.Start();
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var overClose = GetCloseBounds().Contains(e.Location);
        if (overClose != _isCloseHovered)
        {
            _isCloseHovered = overClose;
            Cursor = overClose || IsClickable ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        base.OnMouseMove(e);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left)
        {
            PerformToastClick(e.Location);
        }
        else if (e.Button == MouseButtons.Right)
        {
            BeginFadeOut();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _lifetimeTimer.Dispose();
            _fadeTimer.Dispose();
            _titleFont.Dispose();
            _messageFont.Dispose();
            _logo?.Dispose();
        }

        base.Dispose(disposing);
    }

    private Rectangle GetCloseBounds()
    {
        return new Rectangle(Width - Inset - CloseSize + 6, 8, CloseSize, CloseSize);
    }

    private void BeginFadeOut()
    {
        _lifetimeTimer.Stop();
        _fadingOut = true;
        _fadeTimer.Start();
    }

    private void StepFade()
    {
        if (_fadingOut)
        {
            Opacity = Math.Max(0, Opacity - FadeStep);
            if (Opacity <= 0)
            {
                CloseNow();
            }

            return;
        }

        Opacity = Math.Min(1, Opacity + FadeStep);
        if (Opacity >= 1)
        {
            _fadeTimer.Stop();
        }
    }

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int state);
}
