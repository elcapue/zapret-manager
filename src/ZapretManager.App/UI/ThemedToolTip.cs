namespace ZapretManager.App.UI;

/// <summary>
/// Всплывающая подсказка в теме приложения вместо системной. Разметка текста:
/// у многострочного текста первая строка — заголовок; строка «метка\tзначение» — две колонки;
/// «• …» — абзац-пункт с переносом; пустая строка — отступ.
/// </summary>
internal sealed class ThemedToolTip : IDisposable
{
    private const int Padding = 10;
    private const int MaximumTextWidth = 340;
    private const int ColumnGap = 24;
    private const int ParagraphGap = 6;

    private readonly ToolTip _toolTip = new()
    {
        OwnerDraw = true,
        UseAnimation = false,
        UseFading = false,
        InitialDelay = 350,
        ReshowDelay = 100,
        AutoPopDelay = 30_000
    };

    private readonly Font _titleFont = new("Segoe UI Semibold", 9F, FontStyle.Regular, GraphicsUnit.Point);
    private readonly Font _bodyFont = new("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
    private string _shownText = string.Empty;

    public ThemedToolTip()
    {
        _toolTip.Popup += OnPopup;
        _toolTip.Draw += OnDraw;
    }

    public void SetToolTip(Control control, string? text)
    {
        _toolTip.SetToolTip(control, text);
    }

    /// <summary>Показывает подсказку в заданной точке окна — для частей контрола (строк списка).</summary>
    public void Show(string text, IWin32Window window, Point location)
    {
        _shownText = text;
        _toolTip.Show(text, window, location, _toolTip.AutoPopDelay);
    }

    public void Hide(IWin32Window window)
    {
        _toolTip.Hide(window);
    }

    public void Dispose()
    {
        _toolTip.Dispose();
        _titleFont.Dispose();
        _bodyFont.Dispose();
    }

    internal Size Measure(string text)
    {
        var layout = Layout(text);
        return new Size(layout.Width + Padding * 2, layout.Height + Padding * 2);
    }

    private void OnPopup(object? sender, PopupEventArgs e)
    {
        var text = e.AssociatedControl is null ? null : _toolTip.GetToolTip(e.AssociatedControl);
        e.ToolTipSize = Measure(string.IsNullOrEmpty(text) ? _shownText : text);
    }

    private void OnDraw(object? sender, DrawToolTipEventArgs e)
    {
        using (var back = new SolidBrush(UiTheme.SurfaceRaised))
        {
            e.Graphics.FillRectangle(back, e.Bounds);
        }

        using (var border = new Pen(UiTheme.Border))
        {
            e.Graphics.DrawRectangle(border, 0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);
        }

        var layout = Layout(e.ToolTipText);
        foreach (var block in layout.Blocks)
        {
            var bounds = new Rectangle(Padding, Padding + block.Top, layout.Width, block.Height);
            var flags = TextFormatFlags.NoPrefix | TextFormatFlags.Left;
            switch (block.Kind)
            {
                case BlockKind.Title:
                    TextRenderer.DrawText(e.Graphics, block.Text, _titleFont, bounds, UiTheme.Text, flags | TextFormatFlags.WordBreak);
                    break;
                case BlockKind.Row:
                    TextRenderer.DrawText(e.Graphics, block.Text, _bodyFont, bounds, UiTheme.MutedText, flags);
                    TextRenderer.DrawText(
                        e.Graphics,
                        block.Value,
                        _bodyFont,
                        bounds with { X = Padding + layout.ValueColumn, Width = layout.Width - layout.ValueColumn },
                        UiTheme.Text,
                        flags);
                    break;
                default:
                    TextRenderer.DrawText(e.Graphics, block.Text, _bodyFont, bounds, UiTheme.Text, flags | TextFormatFlags.WordBreak);
                    break;
            }
        }
    }

    private TooltipLayout Layout(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var hasTitle = lines.Length > 1;
        var rows = lines.Where(line => line.Contains('\t')).Select(line => line.Split('\t', 2)).ToArray();
        var labelWidth = rows.Length == 0 ? 0 : rows.Max(row => TextRenderer.MeasureText(row[0], _bodyFont).Width);
        var valueWidth = rows.Length == 0 ? 0 : rows.Max(row => TextRenderer.MeasureText(row[1], _bodyFont).Width);
        var valueColumn = labelWidth == 0 ? 0 : labelWidth + ColumnGap;

        var width = Math.Max(valueColumn + valueWidth, 0);
        foreach (var line in lines.Where(line => !line.Contains('\t') && line.Length > 0))
        {
            var font = hasTitle && line == lines[0] ? _titleFont : _bodyFont;
            width = Math.Max(width, Math.Min(MaximumTextWidth, TextRenderer.MeasureText(line, font).Width));
        }

        var blocks = new List<TooltipBlock>();
        var top = 0;
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (line.Length == 0)
            {
                top += ParagraphGap;
                continue;
            }

            if (line.Contains('\t'))
            {
                var parts = line.Split('\t', 2);
                var rowHeight = TextRenderer.MeasureText(parts[0], _bodyFont).Height;
                blocks.Add(new TooltipBlock(BlockKind.Row, parts[0], parts[1], top, rowHeight));
                top += rowHeight;
                continue;
            }

            var isTitle = hasTitle && index == 0;
            var font = isTitle ? _titleFont : _bodyFont;
            var height = TextRenderer.MeasureText(
                line,
                font,
                new Size(width, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
            blocks.Add(new TooltipBlock(isTitle ? BlockKind.Title : BlockKind.Text, line, string.Empty, top, height));
            top += height + (isTitle ? ParagraphGap : 0);
        }

        return new TooltipLayout(blocks, width, top, valueColumn);
    }

    private enum BlockKind
    {
        Title,
        Row,
        Text
    }

    private sealed record TooltipBlock(BlockKind Kind, string Text, string Value, int Top, int Height);

    private sealed record TooltipLayout(IReadOnlyList<TooltipBlock> Blocks, int Width, int Height, int ValueColumn);
}
