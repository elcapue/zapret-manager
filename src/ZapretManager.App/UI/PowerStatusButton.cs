using System.Drawing.Drawing2D;
using ZapretManager.App.Core;

namespace ZapretManager.App.UI;

/// <summary>
/// Главный переключатель zapret: строка во всю ширину карточки — значок питания, состояние,
/// подпись (время работы и стратегия) и тумблер справа. Кликабельна вся строка.
/// Для внешнего zapret вместо тумблера — «Действия ›»: переключать чужой процесс менеджер не может.
/// </summary>
public sealed class PowerStatusButton : Button
{
    private const int CornerRadius = 12;
    private const int IconDiameter = 40;
    private const int SwitchWidth = 46;
    private const int SwitchHeight = 26;
    private const int Inset = 14;

    private ZapretState _state = ZapretState.RuntimeMissing;
    private string _detailText = string.Empty;
    private bool _isHovered;
    private bool _isPressed;

    public PowerStatusButton()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);
        Size = new Size(422, 68);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleRole = AccessibleRole.PushButton;
        UpdateAccessibility();
    }

    public ZapretState StatusState
    {
        get => _state;
        set
        {
            if (_state == value)
            {
                return;
            }

            _state = value;
            UpdateAccessibility();
            Invalidate();
        }
    }

    /// <summary>Подпись под состоянием: время работы и стратегия, стратегия или пояснение.</summary>
    public string DetailText
    {
        get => _detailText;
        set
        {
            var normalized = value ?? string.Empty;
            if (string.Equals(_detailText, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _detailText = normalized;
            UpdateAccessibility();
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? UiTheme.Surface);
        UiDrawing.ConfigureSurfaceGraphics(e.Graphics);

        var isRunning = _state == ZapretState.Running;
        var isInactive = _state is ZapretState.Stopped or ZapretState.RuntimeMissing;
        var accentColor = GetAccentColor();
        var (backColor, borderColor) = GetSurfaceColors(isRunning);
        UiDrawing.DrawRoundedSurface(
            e.Graphics,
            new RectangleF(0.5F, 0.5F, Width - 1F, Height - 1F),
            CornerRadius,
            backColor,
            borderColor);

        var icon = new RectangleF(Inset, (Height - IconDiameter) / 2F, IconDiameter, IconDiameter);
        using (var iconBack = new SolidBrush(isRunning ? UiTheme.AccentBack : UiTheme.Surface))
        {
            e.Graphics.FillEllipse(iconBack, icon);
        }

        DrawPowerGlyph(e.Graphics, isInactive ? UiTheme.MutedText : accentColor, icon);
        DrawTexts(e.Graphics, (int)icon.Right + 12, isInactive ? UiTheme.Text : accentColor);

        if (_state == ZapretState.External)
        {
            using var actionFont = new Font("Segoe UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point);
            TextRenderer.DrawText(
                e.Graphics,
                "Действия ›",
                actionFont,
                new Rectangle(Width - Inset - 100, 0, 100, Height),
                _isHovered ? UiTheme.Text : UiTheme.StatusNeutral,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
        else if (_state != ZapretState.RuntimeMissing)
        {
            DrawSwitch(e.Graphics, isRunning);
        }

        if (Focused && ShowFocusCues)
        {
            using var focusPath = UiDrawing.CreateRoundedPath(
                new RectangleF(3.5F, 3.5F, Width - 7F, Height - 7F),
                CornerRadius - 3);
            using var focusPen = new Pen(UiTheme.Text) { DashStyle = DashStyle.Dot };
            e.Graphics.DrawPath(focusPen, focusPath);
        }
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

    protected override void OnChangeUICues(UICuesEventArgs e)
    {
        Invalidate();
        base.OnChangeUICues(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        UpdateAccessibility();
        Invalidate();
        base.OnEnabledChanged(e);
    }

    private void DrawTexts(Graphics graphics, int textLeft, Color titleColor)
    {
        const TextFormatFlags flags =
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
            TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        var textWidth = Width - Inset - SwitchWidth - 12 - textLeft;
        var hasDetail = !string.IsNullOrEmpty(_detailText);

        using var titleFont = new Font("Segoe UI Semibold", 11F, FontStyle.Bold, GraphicsUnit.Point);
        TextRenderer.DrawText(
            graphics,
            _state.ToDisplayText(),
            titleFont,
            new Rectangle(textLeft, hasDetail ? Height / 2 - 21 : Height / 2 - 11, textWidth, 22),
            Enabled ? titleColor : UiTheme.DisabledText,
            flags);

        if (!hasDetail)
        {
            return;
        }

        using var detailFont = new Font("Segoe UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point);
        TextRenderer.DrawText(
            graphics,
            _detailText,
            detailFont,
            new Rectangle(textLeft, Height / 2 + 2, textWidth, 18),
            UiTheme.MutedText,
            flags);
    }

    private void DrawSwitch(Graphics graphics, bool isOn)
    {
        var track = new RectangleF(Width - Inset - SwitchWidth, (Height - SwitchHeight) / 2F, SwitchWidth, SwitchHeight);
        var trackColor = isOn ? UiTheme.Accent : UiTheme.Border;
        UiDrawing.DrawRoundedSurface(graphics, track, SwitchHeight / 2F, trackColor, trackColor);

        const float knobInset = 3F;
        var knobSize = SwitchHeight - knobInset * 2;
        var knobX = isOn ? track.Right - knobInset - knobSize : track.X + knobInset;
        using var knob = new SolidBrush(isOn ? UiTheme.WindowBack : UiTheme.StatusNeutral);
        graphics.FillEllipse(knob, knobX, track.Y + knobInset, knobSize, knobSize);
    }

    private static void DrawPowerGlyph(Graphics graphics, Color color, RectangleF icon)
    {
        var centerX = icon.X + icon.Width / 2F;
        var centerY = icon.Y + icon.Height / 2F;
        using var pen = new Pen(color, 2.4F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        graphics.DrawArc(pen, centerX - 9F, centerY - 8F, 18F, 18F, -50F, 280F);
        graphics.DrawLine(pen, centerX, centerY - 11F, centerX, centerY - 1F);
    }

    private Color GetAccentColor()
    {
        return _state switch
        {
            ZapretState.Running => UiTheme.StatusRunning,
            ZapretState.External => UiTheme.StatusExternal,
            _ => UiTheme.StatusNeutral
        };
    }

    private (Color Back, Color Border) GetSurfaceColors(bool isRunning)
    {
        if (!Enabled)
        {
            return (UiTheme.Surface, UiTheme.Border);
        }

        if (isRunning)
        {
            return (_isHovered ? UiTheme.StatusRunningBackHovered : UiTheme.StatusRunningBack, UiTheme.StatusRunningBorder);
        }

        if (_isPressed)
        {
            return (UiTheme.SurfacePressed, UiTheme.Border);
        }

        return _isHovered
            ? (UiTheme.SurfaceHovered, UiTheme.BorderHovered)
            : (UiTheme.SurfaceRaised, UiTheme.Border);
    }

    private void UpdateAccessibility()
    {
        AccessibleName = _state switch
        {
            ZapretState.Running => "Выключить zapret",
            ZapretState.Stopped => "Включить zapret",
            ZapretState.External => "Действия с внешним zapret",
            _ => "zapret недоступен"
        };
        var statusText = _state.ToDisplayText();
        AccessibleDescription = string.IsNullOrWhiteSpace(_detailText)
            ? statusText
            : statusText + ". " + _detailText;
    }
}
