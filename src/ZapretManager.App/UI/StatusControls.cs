using System.Drawing.Drawing2D;

namespace ZapretManager.App.UI;

/// <summary>
/// Сплошной прогресс-бар автовыбора: полоса заполнения, процент,
/// счётчик и текущий проверяемый файл. Заменяет точечный визуализатор.
/// </summary>
public sealed class ScanProgressBar : Control
{
    private int _completed;
    private int _total;
    private string _currentItem = string.Empty;

    /// <summary>Флаг видимости по логике UI (не зависит от создания handle).</summary>
    public bool IsShown { get; private set; }

    public int Completed => _completed;
    public int Total => _total;

    public void ShowBar() => Visible = IsShown = true;

    public void HideBar() => Visible = IsShown = false;

    public ScanProgressBar()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        Size = new Size(396, 42);
        TabStop = false;
    }

    public void SetProgress(int completed, int total, string currentItem)
    {
        _completed = Math.Max(0, completed);
        _total = Math.Max(0, total);
        _currentItem = currentItem;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        UiDrawing.ConfigureSurfaceGraphics(e.Graphics);

        using var monoFont = UiTheme.CreateMonoFont(8F);

        var countText = _total > 0 ? $"{Math.Min(_completed, _total)} / {_total}" : "— / —";
        TextRenderer.DrawText(
            e.Graphics,
            countText,
            monoFont,
            new Rectangle(0, 0, Width / 2, 14),
            UiTheme.MutedText,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        var percent = _total > 0 ? (int)Math.Round(100.0 * Math.Min(_completed, _total) / _total) : 0;
        TextRenderer.DrawText(
            e.Graphics,
            $"{percent}%",
            monoFont,
            new Rectangle(Width / 2, 0, Width - Width / 2, 14),
            UiTheme.MutedText,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        var trackBounds = new RectangleF(0.5F, 18F, Math.Max(1F, Width - 1F), 6F);
        UiDrawing.DrawRoundedSurface(e.Graphics, trackBounds, 3F, UiTheme.SurfaceRaised, UiTheme.SurfaceRaised);

        if (_total > 0 && _completed > 0)
        {
            var fillWidth = Math.Max(6F, trackBounds.Width * Math.Min(_completed, _total) / _total);
            var fillBounds = new RectangleF(trackBounds.X, trackBounds.Y, Math.Min(fillWidth, trackBounds.Width), trackBounds.Height);
            UiDrawing.DrawRoundedSurface(e.Graphics, fillBounds, 3F, UiTheme.Accent, UiTheme.Accent);
        }

        if (!string.IsNullOrEmpty(_currentItem))
        {
            TextRenderer.DrawText(
                e.Graphics,
                _currentItem,
                monoFont,
                new Rectangle(0, 27, Width, 13),
                UiTheme.MutedText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }
    }
}
