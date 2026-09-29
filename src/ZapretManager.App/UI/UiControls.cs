using System.Drawing.Drawing2D;

namespace ZapretManager.App.UI;

internal static class UiDrawing
{
    public const int ControlCornerRadius = 9;
    public const int CardCornerRadius = 14;

    public static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
    {
        return CreateRoundedPath(new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height), radius);
    }

    public static GraphicsPath CreateRoundedPath(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return path;
        }

        var safeRadius = Math.Max(1F, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2F));
        var diameter = safeRadius * 2F;

        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static RectangleF GetInsetBounds(Control control)
    {
        return new RectangleF(
            0.5F,
            0.5F,
            Math.Max(1F, control.Width - 1F),
            Math.Max(1F, control.Height - 1F));
    }

    public static void ConfigureSurfaceGraphics(Graphics graphics)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
    }

    public static void DrawRoundedSurface(
        Graphics graphics,
        RectangleF bounds,
        float radius,
        Color backColor,
        Color borderColor,
        float borderWidth = 1F)
    {
        ConfigureSurfaceGraphics(graphics);
        using var path = CreateRoundedPath(bounds, radius);
        using var backBrush = new SolidBrush(backColor);
        using var borderPen = new Pen(borderColor, borderWidth) { Alignment = PenAlignment.Inset };
        graphics.FillPath(backBrush, path);
        graphics.DrawPath(borderPen, path);
    }
}

public class ThemedButton : Button
{
    private bool _isHovered;
    private bool _isPressed;

    public UiButtonKind Kind { get; set; } = UiButtonKind.Secondary;

    public ThemedButton()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 1;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _isHovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _isHovered = false;
        _isPressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        if (mevent.Button == MouseButtons.Left)
        {
            _isPressed = true;
            Invalidate();
        }

        base.OnMouseDown(mevent);
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        _isPressed = false;
        Invalidate();
        base.OnMouseUp(mevent);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        _isPressed = false;
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var (backColor, borderColor, foreColor) = UiTheme.GetButtonColors(Kind, Enabled, _isPressed, _isHovered);
        e.Graphics.Clear(Parent?.BackColor ?? UiTheme.WindowBack);
        UiDrawing.DrawRoundedSurface(
            e.Graphics,
            UiDrawing.GetInsetBounds(this),
            UiDrawing.ControlCornerRadius,
            backColor,
            borderColor);

        DrawContent(e.Graphics, foreColor);

        if (Focused && ShowFocusCues)
        {
            UiDrawing.ConfigureSurfaceGraphics(e.Graphics);
            using var focusPath = UiDrawing.CreateRoundedPath(
                new RectangleF(3.5F, 3.5F, Math.Max(1F, Width - 7F), Math.Max(1F, Height - 7F)),
                6F);
            using var focusPen = new Pen(foreColor);
            e.Graphics.DrawPath(focusPen, focusPath);
        }
    }

    protected virtual void DrawContent(Graphics graphics, Color foreColor)
    {
        var textBounds = new Rectangle(7, 0, Math.Max(1, Width - 14), Height);
        TextRenderer.DrawText(
            graphics,
            Text,
            Font,
            textBounds,
            foreColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}

internal enum UiIconKind
{
    Update,
    Settings
}

internal sealed class ThemedIconButton : ThemedButton
{
    public UiIconKind IconKind { get; set; }

    protected override void DrawContent(Graphics graphics, Color foreColor)
    {
        UiDrawing.ConfigureSurfaceGraphics(graphics);
        var center = new PointF(Width / 2F, Height / 2F);
        using var pen = new Pen(foreColor, 1.7F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        if (IconKind == UiIconKind.Update)
        {
            // Стрелка «скачать» в лоток: круговая стрелка уже означает перезапуск.
            var x = center.X;
            var y = center.Y;
            graphics.DrawLine(pen, x, y - 7F, x, y + 2.5F);
            graphics.DrawLines(pen, [new PointF(x - 4F, y - 1.5F), new PointF(x, y + 2.5F), new PointF(x + 4F, y - 1.5F)]);
            graphics.DrawLines(
                pen,
                [
                    new PointF(x - 7F, y + 2.5F),
                    new PointF(x - 7F, y + 7F),
                    new PointF(x + 7F, y + 7F),
                    new PointF(x + 7F, y + 2.5F)
                ]);
            return;
        }

        graphics.DrawEllipse(pen, center.X - 5.2F, center.Y - 5.2F, 10.4F, 10.4F);
        graphics.DrawEllipse(pen, center.X - 1.8F, center.Y - 1.8F, 3.6F, 3.6F);
        for (var index = 0; index < 8; index++)
        {
            var angle = index * Math.PI / 4D;
            var inner = new PointF(
                center.X + (float)Math.Cos(angle) * 6.5F,
                center.Y + (float)Math.Sin(angle) * 6.5F);
            var outer = new PointF(
                center.X + (float)Math.Cos(angle) * 8.5F,
                center.Y + (float)Math.Sin(angle) * 8.5F);
            graphics.DrawLine(pen, inner, outer);
        }
    }
}

/// <summary>
/// Знак приложения в плашке той же формы, что иконки-кнопки шапки: лого выглядит частью интерфейса,
/// а не картинкой поверх него. Не кликабелен и не получает фокус.
/// </summary>
internal sealed class AppLogoTile : Control
{
    private const int MarkInset = 4;
    private readonly Bitmap? _mark = AppAssets.LoadMark();

    public AppLogoTile()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
        AccessibleRole = AccessibleRole.Graphic;
        AccessibleName = "Логотип Zapret Manager";
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var (backColor, borderColor, _) = UiTheme.GetButtonColors(UiButtonKind.Ghost, enabled: true, isPressed: false, isHovered: false);
        e.Graphics.Clear(Parent?.BackColor ?? UiTheme.WindowBack);
        UiDrawing.DrawRoundedSurface(
            e.Graphics,
            UiDrawing.GetInsetBounds(this),
            UiDrawing.ControlCornerRadius,
            backColor,
            borderColor);

        if (_mark is not null)
        {
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.DrawImage(_mark, Rectangle.Inflate(ClientRectangle, -MarkInset, -MarkInset));
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _mark?.Dispose();
        }

        base.Dispose(disposing);
    }
}

internal sealed class ThemedSectionPanel : Panel
{
    private readonly Label _titleLabel;

    public ThemedSectionPanel()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        _titleLabel = new Label
        {
            Name = "SectionTitleLabel",
            // По ширине текста: справа в строке заголовка могут стоять действия секции.
            AutoSize = true,
            Location = new Point(14, 10)
        };
        UiTheme.StyleSectionTitle(_titleLabel);
        Controls.Add(_titleLabel);
    }

    public string Title
    {
        get => _titleLabel.Text;
        set => _titleLabel.Text = UiTheme.ToEyebrowText(value);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? UiTheme.WindowBack);
        UiDrawing.DrawRoundedSurface(
            e.Graphics,
            UiDrawing.GetInsetBounds(this),
            UiDrawing.CardCornerRadius,
            BackColor,
            UiTheme.Border);
    }
}

internal sealed class StatusPulseIndicator : Control
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 45 };
    private double _phase;

    public Color IndicatorColor { get; set; } = UiTheme.StatusNeutral;

    public bool IsActive
    {
        get => _timer.Enabled;
        set
        {
            if (value)
            {
                _timer.Start();
            }
            else
            {
                _timer.Stop();
            }

            Invalidate();
        }
    }

    public StatusPulseIndicator()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        _timer.Tick += (_, _) =>
        {
            _phase = (_phase + 0.045) % (Math.PI * 2);
            Invalidate();
        };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var center = new PointF(Width / 2F, Height / 2F);
        var pulse = IsActive ? (float)((Math.Sin(_phase) + 1) / 2) : 0F;
        var glowRadius = 14F + pulse * 4F;
        var glowColor = Color.FromArgb((int)(18 + pulse * 20), IndicatorColor);
        using var glowBrush = new SolidBrush(glowColor);
        using var dotBrush = new SolidBrush(IndicatorColor);
        e.Graphics.FillEllipse(glowBrush, center.X - glowRadius, center.Y - glowRadius, glowRadius * 2, glowRadius * 2);
        e.Graphics.FillEllipse(dotBrush, center.X - 6, center.Y - 6, 12, 12);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
        }

        base.Dispose(disposing);
    }
}

