using ZapretManager.App.Core;

namespace ZapretManager.App.UI;

internal sealed class StrategyPickerList : Control
{
    private const int ItemHeight = 32;
    private const int ScrollBarWidth = 8;
    private readonly IReadOnlyList<StrategyInfo> _items;
    private readonly Action<int> _onCommit;
    private readonly Action<Keys> _onKey;
    private int _firstVisibleIndex;
    private int _highlightedIndex;
    private int _selectedIndex;
    private int _visibleItemCount = 1;

    public StrategyPickerList(
        IReadOnlyList<StrategyInfo> items,
        int selectedIndex,
        Action<int> onCommit,
        Action<Keys> onKey)
    {
        _items = items;
        _selectedIndex = selectedIndex >= 0 && selectedIndex < items.Count ? selectedIndex : -1;
        _highlightedIndex = _selectedIndex >= 0 ? _selectedIndex : 0;
        _onCommit = onCommit;
        _onKey = onKey;

        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);
        BackColor = UiTheme.Surface;
        Font = UiTheme.GetFont(9F);
        TabStop = true;
        AccessibleRole = AccessibleRole.List;
        AccessibleName = "Доступные стратегии";
    }

    public void SetVisibleItemCount(int count)
    {
        _visibleItemCount = Math.Max(1, count);
        EnsureVisible(_highlightedIndex);
        Invalidate();
    }

    public void MoveHighlight(int offset)
    {
        if (_items.Count == 0)
        {
            return;
        }

        _highlightedIndex = Math.Clamp(_highlightedIndex + offset, 0, _items.Count - 1);
        EnsureVisible(_highlightedIndex);
        UpdateAccessibleDescription();
        Invalidate();
    }

    public void MoveHighlightToBoundary(bool first)
    {
        if (_items.Count == 0)
        {
            return;
        }

        _highlightedIndex = first ? 0 : _items.Count - 1;
        EnsureVisible(_highlightedIndex);
        UpdateAccessibleDescription();
        Invalidate();
    }

    public void CommitHighlight()
    {
        if (_highlightedIndex >= 0 && _highlightedIndex < _items.Count)
        {
            _selectedIndex = _highlightedIndex;
            _onCommit(_highlightedIndex);
        }
    }

    protected override bool IsInputKey(Keys keyData)
    {
        return (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Home or Keys.End ||
            base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.Enter or Keys.Escape)
        {
            _onKey(e.KeyCode);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        base.OnKeyDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var index = _firstVisibleIndex + Math.Max(0, (e.Y - 1) / ItemHeight);
        if (index >= 0 && index < _items.Count && index < _firstVisibleIndex + _visibleItemCount)
        {
            _highlightedIndex = index;
            UpdateAccessibleDescription();
            Invalidate();
        }

        base.OnMouseMove(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            var index = _firstVisibleIndex + Math.Max(0, (e.Y - 1) / ItemHeight);
            if (index >= 0 && index < _items.Count && index < _firstVisibleIndex + _visibleItemCount)
            {
                _highlightedIndex = index;
                CommitHighlight();
            }
        }

        base.OnMouseDown(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (_items.Count > _visibleItemCount)
        {
            var direction = e.Delta > 0 ? -1 : 1;
            _firstVisibleIndex = Math.Clamp(
                _firstVisibleIndex + direction,
                0,
                Math.Max(0, _items.Count - _visibleItemCount));
            _highlightedIndex = Math.Clamp(
                _highlightedIndex,
                _firstVisibleIndex,
                Math.Min(_items.Count - 1, _firstVisibleIndex + _visibleItemCount - 1));
            UpdateAccessibleDescription();
            Invalidate();
        }

        base.OnMouseWheel(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(UiTheme.Surface);
        UiDrawing.DrawRoundedSurface(
            e.Graphics,
            UiDrawing.GetInsetBounds(this),
            UiDrawing.ControlCornerRadius,
            UiTheme.Surface,
            UiTheme.Border);

        var contentWidth = Width - (_items.Count > _visibleItemCount ? ScrollBarWidth + 9 : 7);
        var lastIndex = Math.Min(_items.Count, _firstVisibleIndex + _visibleItemCount);
        for (var index = _firstVisibleIndex; index < lastIndex; index++)
        {
            DrawItem(e.Graphics, index, contentWidth);
        }

        if (_items.Count > _visibleItemCount)
        {
            DrawScrollBar(e.Graphics);
        }
    }

    private void DrawItem(Graphics graphics, int index, int contentWidth)
    {
        var visualIndex = index - _firstVisibleIndex;
        var bounds = new Rectangle(5, 2 + visualIndex * ItemHeight, Math.Max(1, contentWidth - 5), ItemHeight - 2);
        var isSelected = index == _selectedIndex;
        var isHighlighted = index == _highlightedIndex;

        if (isSelected || isHighlighted)
        {
            var backColor = isSelected
                ? UiTheme.AccentBack
                : UiTheme.SurfaceRaised;
            using var path = UiDrawing.CreateRoundedPath(new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height), 7F);
            using var brush = new SolidBrush(backColor);
            graphics.FillPath(brush, path);
        }

        if (isSelected)
        {
            using var accentBrush = new SolidBrush(UiTheme.Accent);
            graphics.FillRectangle(accentBrush, bounds.Left + 2, bounds.Top + 7, 3, Math.Max(4, bounds.Height - 14));
        }

        var textBounds = new Rectangle(bounds.Left + 12, bounds.Top, Math.Max(1, bounds.Width - 18), bounds.Height);
        TextRenderer.DrawText(
            graphics,
            _items[index].DisplayName,
            Font,
            textBounds,
            isSelected ? UiTheme.Accent : UiTheme.Text,
            TextFormatFlags.Left |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix |
            TextFormatFlags.SingleLine);
    }

    private void DrawScrollBar(Graphics graphics)
    {
        var trackBounds = new RectangleF(Width - 8F, 8F, 3F, Math.Max(8F, Height - 16F));
        var thumbHeight = Math.Max(18F, trackBounds.Height * _visibleItemCount / _items.Count);
        var maxFirstIndex = Math.Max(1, _items.Count - _visibleItemCount);
        var thumbTop = trackBounds.Top + (trackBounds.Height - thumbHeight) * _firstVisibleIndex / maxFirstIndex;
        using var trackBrush = new SolidBrush(UiTheme.ScrollTrack);
        using var thumbBrush = new SolidBrush(UiTheme.ScrollThumb);
        graphics.FillRectangle(trackBrush, trackBounds);
        graphics.FillRectangle(thumbBrush, trackBounds.X, thumbTop, trackBounds.Width, thumbHeight);
    }

    private void EnsureVisible(int index)
    {
        if (index < _firstVisibleIndex)
        {
            _firstVisibleIndex = index;
        }
        else if (index >= _firstVisibleIndex + _visibleItemCount)
        {
            _firstVisibleIndex = index - _visibleItemCount + 1;
        }

        _firstVisibleIndex = Math.Clamp(
            _firstVisibleIndex,
            0,
            Math.Max(0, _items.Count - _visibleItemCount));
    }

    private void UpdateAccessibleDescription()
    {
        AccessibleDescription = _highlightedIndex >= 0 && _highlightedIndex < _items.Count
            ? $"{_highlightedIndex + 1} из {_items.Count}: {_items[_highlightedIndex].DisplayName}"
            : "Список стратегий пуст.";
    }
}
