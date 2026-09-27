using System.Drawing.Drawing2D;

namespace ZapretManager.App.UI;

public sealed class StrategyResultsList : Control
{
    private const int MaximumRows = 3;
    private readonly List<ResultRow> _items = [];
    private readonly ThemedToolTip _toolTip = new();
    private int _hoveredRow = -1;

    public sealed record ResultRow(string Name, string Metric, string Details = "");

    public StrategyResultsList()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);

        BackColor = Color.Transparent;
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        TabStop = false;
        AccessibleRole = AccessibleRole.List;
        AccessibleName = "Лучшие стратегии последнего сканирования";
    }

    public IReadOnlyList<string> Items => _items.Select(item => item.Name).ToArray();

    internal IReadOnlyList<ResultRow> Rows => _items.ToArray();

    public void SetItems(IEnumerable<string> items)
    {
        SetRows(items.Select(name => new ResultRow(name, string.Empty)));
    }

    public void SetRows(IEnumerable<ResultRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        _items.Clear();
        _items.AddRange(rows.Where(item => !string.IsNullOrWhiteSpace(item.Name)).Take(MaximumRows));
        SetHoveredRow(-1);
        Text = string.Join(Environment.NewLine, _items.Select(item => item.Name));
        AccessibleDescription = _items.Count == 0
            ? "Список пока пуст."
            : string.Join(", ", _items.Select((item, index) => $"{index + 1}: {item.Name} {item.Metric}".TrimEnd()));
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_items.Count == 0)
        {
            return;
        }

        UiDrawing.ConfigureSurfaceGraphics(e.Graphics);
        var rowHeight = Math.Max(20F, Height / (float)_items.Count);
        using var nameFont = UiTheme.CreateMonoFont(8.5F);
        using var metricFont = UiTheme.CreateMonoFont(8F);

        for (var index = 0; index < _items.Count; index++)
        {
            var rowBounds = new RectangleF(0F, index * rowHeight, Math.Max(1F, Width), rowHeight);
            if (index == _hoveredRow)
            {
                using var hoverPath = UiDrawing.CreateRoundedPath(rowBounds, 6F);
                using var hoverBrush = new SolidBrush(UiTheme.SurfaceHovered);
                e.Graphics.FillPath(hoverBrush, hoverPath);
            }

            DrawRank(e.Graphics, index + 1, rowBounds);
            DrawStrategyName(e.Graphics, _items[index].Name, nameFont, rowBounds);
            if (!string.IsNullOrEmpty(_items[index].Metric))
            {
                DrawMetric(e.Graphics, _items[index].Metric, metricFont, rowBounds);
            }

            if (index < _items.Count - 1)
            {
                DrawSeparator(e.Graphics, rowBounds.Bottom);
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var row = _items.Count == 0 ? -1 : Math.Clamp(e.Y * _items.Count / Math.Max(1, Height), 0, _items.Count - 1);
        SetHoveredRow(row);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetHoveredRow(-1);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>Подробности по стратегии — в подсказке под строкой, на которую навели курсор.</summary>
    private void SetHoveredRow(int row)
    {
        if (row == _hoveredRow)
        {
            return;
        }

        _hoveredRow = row;
        Invalidate();
        if (row < 0 || string.IsNullOrEmpty(_items[row].Details))
        {
            _toolTip.Hide(this);
            return;
        }

        var rowBottom = (row + 1) * Height / _items.Count;
        _toolTip.Show(_items[row].Details, this, new Point(28, rowBottom + 4));
    }

    private void DrawRank(Graphics graphics, int rank, RectangleF rowBounds)
    {
        var textBounds = Rectangle.Round(new RectangleF(
            rowBounds.Left,
            rowBounds.Top,
            22F,
            rowBounds.Height));

        TextRenderer.DrawText(
            graphics,
            rank.ToString(),
            Font,
            textBounds,
            UiTheme.MutedText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    private void DrawStrategyName(Graphics graphics, string name, Font font, RectangleF rowBounds)
    {
        var textBounds = Rectangle.Round(new RectangleF(
            rowBounds.Left + 28F,
            rowBounds.Top,
            Math.Max(1F, rowBounds.Width - 188F),
            rowBounds.Height));

        TextRenderer.DrawText(
            graphics,
            name,
            font,
            textBounds,
            UiTheme.Text,
            TextFormatFlags.Left |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix |
            TextFormatFlags.SingleLine);
    }

    private void DrawMetric(Graphics graphics, string metric, Font font, RectangleF rowBounds)
    {
        var textBounds = Rectangle.Round(new RectangleF(
            rowBounds.Left + 28F,
            rowBounds.Top,
            Math.Max(1F, rowBounds.Width - 32F),
            rowBounds.Height));

        TextRenderer.DrawText(
            graphics,
            metric,
            font,
            textBounds,
            UiTheme.Accent,
            TextFormatFlags.Right |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPrefix |
            TextFormatFlags.SingleLine);
    }

    private void DrawSeparator(Graphics graphics, float y)
    {
        using var separatorPen = new Pen(Color.FromArgb(90, UiTheme.Border));
        graphics.DrawLine(separatorPen, 28F, y - 0.5F, Math.Max(28F, Width - 4F), y - 0.5F);
    }
}
