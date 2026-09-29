using ZapretManager.App.Core;

namespace ZapretManager.App.UI;

public sealed class StrategyPicker : Control
{
    private readonly List<StrategyInfo> _items = [];
    private StrategyPickerDropDown? _dropDown;
    private int _selectedIndex = -1;
    private bool _isHovered;
    private bool _isPressed;
    private bool _selectionCommitPending;
    private bool _pendingSelectionChanged;

    public StrategyPicker()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);

        BackColor = UiTheme.SurfaceRaised;
        ForeColor = UiTheme.Text;
        Font = UiTheme.GetFont(9F);
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleRole = AccessibleRole.ComboBox;
        AccessibleName = "Стратегия";
        AccessibleDescription = "Выбор стратегии zapret. Используйте стрелки вверх и вниз для выбора.";
    }

    public event EventHandler? SelectedItemChanged;

    public IReadOnlyList<StrategyInfo> Items => _items;

    public StrategyInfo? SelectedItem => _selectedIndex >= 0 && _selectedIndex < _items.Count
        ? _items[_selectedIndex]
        : null;

    public bool IsDropDownOpen => _dropDown is not null;

    public void SetItems(IEnumerable<StrategyInfo> items, StrategyInfo? selectedItem)
    {
        ArgumentNullException.ThrowIfNull(items);

        var selectedPath = selectedItem?.FullPath;
        _items.Clear();
        _items.AddRange(items);
        _selectedIndex = selectedPath is null
            ? -1
            : _items.FindIndex(item => string.Equals(item.FullPath, selectedPath, StringComparison.OrdinalIgnoreCase));

        CloseDropDown();
        UpdateAccessibleValue();
        Invalidate();
    }

    public void OpenDropDown()
    {
        if (!Enabled || _items.Count == 0 || _dropDown is not null)
        {
            return;
        }

        Focus();
        _dropDown = new StrategyPickerDropDown(
            this,
            _items,
            _selectedIndex,
            CommitSelection,
            HandlePopupKey);
        _dropDown.Closed += DropDownClosed;
        _dropDown.ShowForOwner();
        Invalidate();
    }

    public void CloseDropDown()
    {
        _dropDown?.Close(ToolStripDropDownCloseReason.CloseCalled);
    }

    protected override bool IsInputKey(Keys keyData)
    {
        return (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Home or Keys.End ||
            base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (HandlePickerKey(e.KeyCode, e.Alt))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        base.OnKeyDown(e);
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

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _isPressed = true;
            Focus();
            Invalidate();
        }

        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _isPressed = false;
            if (ClientRectangle.Contains(e.Location))
            {
                if (IsDropDownOpen)
                {
                    CloseDropDown();
                }
                else
                {
                    OpenDropDown();
                }
            }

            Invalidate();
        }

        base.OnMouseUp(e);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        Invalidate();
        base.OnGotFocus(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        Invalidate();
        base.OnLostFocus(e);
    }

    protected override void OnChangeUICues(UICuesEventArgs e)
    {
        Invalidate();
        base.OnChangeUICues(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        _isPressed = false;
        if (!Enabled)
        {
            CloseDropDown();
        }

        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? UiTheme.WindowBack);

        var backColor = Enabled
            ? (_isPressed ? UiTheme.SurfacePressed : UiTheme.SurfaceRaised)
            : UiTheme.Surface;
        // Акцентная рамка — пока открыт список или при навигации с клавиатуры. После клика мышью
        // фокус остаётся на контроле, но подсвечивать его незачем: это только мозолит глаза.
        var borderColor = !Enabled
            ? UiTheme.Border
            : IsDropDownOpen || (Focused && ShowFocusCues)
                ? UiTheme.Accent
                : _isHovered
                    ? UiTheme.BorderHovered
                    : UiTheme.Border;
        var textColor = Enabled ? UiTheme.Text : UiTheme.DisabledText;

        UiDrawing.DrawRoundedSurface(
            e.Graphics,
            UiDrawing.GetInsetBounds(this),
            UiDrawing.ControlCornerRadius,
            backColor,
            borderColor);

        var textBounds = new Rectangle(12, 0, Math.Max(1, Width - 48), Height);
        TextRenderer.DrawText(
            e.Graphics,
            SelectedItem?.DisplayName ?? (_items.Count == 0 ? "Стратегии не найдены" : "Выберите стратегию"),
            Font,
            textBounds,
            textColor,
            TextFormatFlags.Left |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix |
            TextFormatFlags.SingleLine);

        DrawArrow(e.Graphics, textColor);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _dropDown is not null)
        {
            _dropDown.Closed -= DropDownClosed;
            _dropDown.Dispose();
            _dropDown = null;
        }

        base.Dispose(disposing);
    }

    private bool HandlePickerKey(Keys keyCode, bool altPressed)
    {
        if (!Enabled || _items.Count == 0)
        {
            return false;
        }

        if (keyCode == Keys.F4 || keyCode == Keys.Enter || keyCode == Keys.Space || (altPressed && keyCode == Keys.Down))
        {
            if (IsDropDownOpen)
            {
                CloseDropDown();
            }
            else
            {
                OpenDropDown();
            }

            return true;
        }

        if (keyCode == Keys.Escape && IsDropDownOpen)
        {
            CloseDropDown();
            return true;
        }

        if (keyCode == Keys.Up || keyCode == Keys.Down || keyCode == Keys.Home || keyCode == Keys.End)
        {
            var targetIndex = keyCode switch
            {
                Keys.Home => 0,
                Keys.End => _items.Count - 1,
                Keys.Up => Math.Max(0, _selectedIndex - 1),
                _ => Math.Min(_items.Count - 1, _selectedIndex + 1)
            };
            SetSelectedIndex(targetIndex, raiseEvent: true);
            return true;
        }

        return false;
    }

    private void HandlePopupKey(Keys keyCode)
    {
        if (_dropDown is null)
        {
            return;
        }

        switch (keyCode)
        {
            case Keys.Up:
                _dropDown.MoveHighlight(-1);
                break;
            case Keys.Down:
                _dropDown.MoveHighlight(1);
                break;
            case Keys.Home:
                _dropDown.MoveHighlightToBoundary(first: true);
                break;
            case Keys.End:
                _dropDown.MoveHighlightToBoundary(first: false);
                break;
            case Keys.Enter:
                _dropDown.CommitHighlight();
                break;
            case Keys.Escape:
                CloseDropDown();
                break;
        }
    }

    private void CommitSelection(int index)
    {
        if (index < 0 || index >= _items.Count)
        {
            return;
        }

        _pendingSelectionChanged |= index != _selectedIndex;
        SetSelectedIndex(index, raiseEvent: false);
        if (_selectionCommitPending)
        {
            return;
        }

        _selectionCommitPending = true;
        if (IsHandleCreated && !IsDisposed)
        {
            BeginInvoke(new Action(CompletePendingSelection));
            return;
        }

        CompletePendingSelection();
    }

    private void CompletePendingSelection()
    {
        if (IsDisposed)
        {
            return;
        }

        var raiseEvent = _pendingSelectionChanged;
        _selectionCommitPending = false;
        _pendingSelectionChanged = false;
        CloseDropDown();
        Focus();
        if (raiseEvent)
        {
            SelectedItemChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SetSelectedIndex(int index, bool raiseEvent)
    {
        if (index < 0 || index >= _items.Count || index == _selectedIndex)
        {
            return;
        }

        _selectedIndex = index;
        UpdateAccessibleValue();
        Invalidate();
        if (raiseEvent)
        {
            SelectedItemChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void DropDownClosed(object? sender, ToolStripDropDownClosedEventArgs e)
    {
        if (_dropDown is null)
        {
            return;
        }

        var closedDropDown = _dropDown;
        _dropDown = null;
        closedDropDown.Closed -= DropDownClosed;
        _isPressed = false;
        Invalidate();

        if (IsHandleCreated && !IsDisposed)
        {
            BeginInvoke(new Action(closedDropDown.Dispose));
        }
        else
        {
            closedDropDown.Dispose();
        }
    }

    private void DrawArrow(Graphics graphics, Color color)
    {
        UiDrawing.ConfigureSurfaceGraphics(graphics);
        var centerX = Width - 20F;
        var centerY = Height / 2F;
        var points = IsDropDownOpen
            ? new[] { new PointF(centerX - 4F, centerY + 2F), new PointF(centerX, centerY - 2F), new PointF(centerX + 4F, centerY + 2F) }
            : new[] { new PointF(centerX - 4F, centerY - 2F), new PointF(centerX, centerY + 2F), new PointF(centerX + 4F, centerY - 2F) };
        using var pen = new Pen(color, 1.6F)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round,
            LineJoin = System.Drawing.Drawing2D.LineJoin.Round
        };
        graphics.DrawLines(pen, points);
    }

    private void UpdateAccessibleValue()
    {
        AccessibleDescription = SelectedItem is null
            ? (_items.Count == 0 ? "Стратегии не найдены." : "Стратегия не выбрана.")
            : $"Выбрана стратегия {SelectedItem.DisplayName}. Используйте стрелки вверх и вниз для выбора.";
    }
}

internal sealed class StrategyPickerDropDown : ToolStripDropDown
{
    private const int ItemHeight = 32;
    private const int MaximumVisibleItems = 7;
    private readonly StrategyPicker _owner;
    private readonly StrategyPickerList _list;
    private readonly ToolStripControlHost _host;
    private readonly int _itemCount;

    public StrategyPickerDropDown(
        StrategyPicker owner,
        IReadOnlyList<StrategyInfo> items,
        int selectedIndex,
        Action<int> onCommit,
        Action<Keys> onKey)
    {
        _owner = owner;
        _itemCount = items.Count;
        AutoClose = true;
        AutoSize = false;
        BackColor = UiTheme.Surface;
        DropShadowEnabled = true;
        Padding = Padding.Empty;
        Margin = Padding.Empty;
        Renderer = new ToolStripProfessionalRenderer(new StrategyPickerColorTable());

        _list = new StrategyPickerList(items, selectedIndex, onCommit, onKey)
        {
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        _host = new ToolStripControlHost(_list)
        {
            AutoSize = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        Items.Add(_host);
    }

    public void ShowForOwner()
    {
        var fieldBounds = new Rectangle(_owner.PointToScreen(Point.Empty), _owner.Size);
        var workingBounds = Screen.FromControl(_owner).WorkingArea;
        var form = _owner.FindForm();
        var formBounds = form is null
            ? workingBounds
            : new Rectangle(form.PointToScreen(Point.Empty), form.ClientSize);
        var allowedBounds = Rectangle.Intersect(workingBounds, formBounds);
        if (allowedBounds.Width < _owner.Width || allowedBounds.Height < ItemHeight + 2)
        {
            allowedBounds = workingBounds;
        }

        var bounds = CalculateBounds(fieldBounds, allowedBounds, _owner.Width, _itemCount);
        Size = bounds.Size;
        _host.Size = bounds.Size;
        _list.Size = bounds.Size;
        _list.SetVisibleItemCount(Math.Max(1, (bounds.Height - 2) / ItemHeight));
        ApplyRoundedRegion();
        Show(bounds.Location);
        _list.BeginInvoke(_list.Focus);
    }

    public void MoveHighlight(int offset) => _list.MoveHighlight(offset);

    public void MoveHighlightToBoundary(bool first) => _list.MoveHighlightToBoundary(first);

    public void CommitHighlight() => _list.CommitHighlight();

    internal static Rectangle CalculateBounds(Rectangle fieldBounds, Rectangle allowedBounds, int preferredWidth, int itemCount)
    {
        var width = Math.Min(Math.Max(1, preferredWidth), allowedBounds.Width);
        var desiredVisibleItems = Math.Max(1, Math.Min(itemCount, MaximumVisibleItems));
        var desiredHeight = desiredVisibleItems * ItemHeight + 2;
        var spaceBelow = Math.Max(0, allowedBounds.Bottom - fieldBounds.Bottom);
        var spaceAbove = Math.Max(0, fieldBounds.Top - allowedBounds.Top);
        var openBelow = spaceBelow >= desiredHeight || spaceBelow >= spaceAbove;
        var availableHeight = openBelow ? spaceBelow : spaceAbove;
        var visibleItems = Math.Max(1, Math.Min(desiredVisibleItems, Math.Max(1, (availableHeight - 2) / ItemHeight)));
        var height = Math.Min(allowedBounds.Height, visibleItems * ItemHeight + 2);
        var x = Math.Clamp(fieldBounds.Left, allowedBounds.Left, Math.Max(allowedBounds.Left, allowedBounds.Right - width));
        var y = openBelow
            ? Math.Min(fieldBounds.Bottom + 4, allowedBounds.Bottom - height)
            : Math.Max(allowedBounds.Top, fieldBounds.Top - height - 4);

        return new Rectangle(x, y, width, height);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        ApplyRoundedRegion();
    }

    private void ApplyRoundedRegion()
    {
        if (Width <= 0 || Height <= 0)
        {
            return;
        }

        using var path = UiDrawing.CreateRoundedPath(new Rectangle(0, 0, Width, Height), UiDrawing.ControlCornerRadius);
        var previousRegion = Region;
        Region = new Region(path);
        previousRegion?.Dispose();
    }

    private sealed class StrategyPickerColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => UiTheme.Surface;
        public override Color MenuBorder => UiTheme.Border;
        public override Color ImageMarginGradientBegin => UiTheme.Surface;
        public override Color ImageMarginGradientMiddle => UiTheme.Surface;
        public override Color ImageMarginGradientEnd => UiTheme.Surface;
    }
}
