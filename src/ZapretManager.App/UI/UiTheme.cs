using System.Runtime.InteropServices;

namespace ZapretManager.App.UI;

public enum UiButtonKind
{
    Primary,
    Secondary,
    Danger,
    Ghost,
    Subtle
}

public static class UiTheme
{
    public const int DialogWidth = 520;
    public const int DialogHeight = 220;
    public const int DialogPadding = 24;
    public const int DialogButtonHeight = 34;
    public const int DialogButtonGap = 10;

    public static readonly Color WindowBack = Color.FromArgb(10, 10, 10);
    public static readonly Color Surface = Color.FromArgb(17, 17, 17);
    public static readonly Color SurfaceRaised = Color.FromArgb(22, 22, 22);
    public static readonly Color SurfaceHovered = Color.FromArgb(32, 32, 32);
    public static readonly Color SurfacePressed = Color.FromArgb(13, 13, 13);
    public static readonly Color Border = Color.FromArgb(48, 48, 48);
    public static readonly Color BorderHovered = Color.FromArgb(70, 70, 70);
    public static readonly Color Text = Color.FromArgb(230, 231, 230);
    public static readonly Color MutedText = Color.FromArgb(123, 118, 123);
    public static readonly Color DisabledText = Color.FromArgb(111, 111, 111);
    public static readonly Color ScrollTrack = Color.FromArgb(31, 31, 31);
    public static readonly Color ScrollThumb = Color.FromArgb(95, 95, 95);
    public static readonly Color Accent = Color.FromArgb(124, 255, 74);
    public static readonly Color AccentBack = Color.FromArgb(29, 52, 20);
    public static readonly Color AccentPressed = Color.FromArgb(83, 216, 43);
    public static readonly Color Danger = Color.FromArgb(255, 70, 85);
    public static readonly Color DangerPressed = Color.FromArgb(217, 47, 61);
    public static readonly Color StatusRunning = Accent;
    public static readonly Color StatusRunningBack = Color.FromArgb(20, 34, 16);
    public static readonly Color StatusRunningBackHovered = Color.FromArgb(26, 44, 20);
    public static readonly Color StatusRunningBorder = Color.FromArgb(52, 96, 36);
    public static readonly Color StatusExternal = Color.FromArgb(255, 196, 70);
    public static readonly Color StatusNeutral = Color.FromArgb(198, 198, 198);

    public static void ApplyWindow(Form form)
    {
        form.BackColor = WindowBack;
        form.ForeColor = Text;
        form.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        form.HandleCreated += (_, _) => ApplyDarkTitleBar(form);
        form.Activated += (_, _) => ApplyDarkTitleBar(form);
        form.Deactivate += (_, _) => ApplyDarkTitleBar(form);

        if (form.IsHandleCreated)
        {
            ApplyDarkTitleBar(form);
        }
    }

    public static void ApplyDialogWindow(
        Form form,
        FormStartPosition startPosition = FormStartPosition.CenterParent,
        bool showInTaskbar = false)
    {
        form.Icon = AppAssets.LoadWindowIcon();
        form.StartPosition = startPosition;
        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        form.MaximizeBox = false;
        form.MinimizeBox = false;
        form.ShowInTaskbar = showInTaskbar;
        ApplyWindow(form);
    }

    internal static void StyleSection(ThemedSectionPanel panel)
    {
        panel.BackColor = Surface;
        panel.ForeColor = Text;
        panel.BorderStyle = BorderStyle.None;
    }

    /// <summary>Eyebrow-заголовок секции: капс, разрядка, muted (стиль мокапа).</summary>
    public static void StyleSectionTitle(Label label)
    {
        label.BackColor = Color.Transparent;
        label.ForeColor = MutedText;
        label.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold, GraphicsUnit.Point);
        label.UseCompatibleTextRendering = false;
    }

    /// <summary>Капс для eyebrow-заголовков секций.</summary>
    public static string ToEyebrowText(string text)
    {
        return text.ToUpperInvariant();
    }

    public static void StyleLabel(Label label, Color? color = null, float size = 10F, FontStyle style = FontStyle.Regular)
    {
        label.BackColor = Color.Transparent;
        label.ForeColor = color ?? Text;
        label.Font = new Font("Segoe UI", size, style, GraphicsUnit.Point);
    }

    public static void StyleButton(Button button, UiButtonKind kind = UiButtonKind.Secondary)
    {
        if (button is ThemedButton themedButton)
        {
            themedButton.Kind = kind;
        }

        var (backColor, borderColor, foreColor) = GetButtonColors(kind, button.Enabled, isPressed: false, isHovered: false);

        button.BackColor = backColor;
        button.ForeColor = foreColor;
        button.FlatStyle = FlatStyle.Flat;
        button.UseVisualStyleBackColor = false;
        button.FlatAppearance.BorderSize = kind == UiButtonKind.Subtle ? 0 : 1;
        button.FlatAppearance.BorderColor = borderColor;
        button.FlatAppearance.MouseOverBackColor = kind switch
        {
            UiButtonKind.Primary => Color.FromArgb(39, 65, 28),
            UiButtonKind.Danger => Color.FromArgb(67, 29, 35),
            _ => SurfaceHovered
        };
        button.FlatAppearance.MouseDownBackColor = kind switch
        {
            UiButtonKind.Primary => AccentPressed,
            UiButtonKind.Danger => DangerPressed,
            _ => SurfacePressed
        };
        button.Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold, GraphicsUnit.Point);
        button.EnabledChanged += (_, _) =>
        {
            var colors = GetButtonColors(kind, button.Enabled, isPressed: false, isHovered: false);
            button.BackColor = colors.BackColor;
            button.ForeColor = colors.ForeColor;
            button.FlatAppearance.BorderColor = colors.BorderColor;
        };
    }

    public static (Color BackColor, Color BorderColor, Color ForeColor) GetButtonColors(
        UiButtonKind kind,
        bool enabled,
        bool isPressed,
        bool isHovered)
    {
        if (!enabled)
        {
            return kind switch
            {
                UiButtonKind.Primary => (Color.FromArgb(20, 29, 17), Color.FromArgb(65, 91, 54), DisabledText),
                UiButtonKind.Danger => (Color.FromArgb(31, 19, 21), Color.FromArgb(103, 45, 51), DisabledText),
                _ => (Surface, Color.FromArgb(42, 42, 42), DisabledText)
            };
        }

        return kind switch
        {
            UiButtonKind.Primary when isPressed => (Color.FromArgb(24, 43, 17), AccentPressed, Text),
            UiButtonKind.Primary when isHovered => (Color.FromArgb(39, 65, 28), Accent, Text),
            UiButtonKind.Primary => (AccentBack, Accent, Text),
            UiButtonKind.Danger when isPressed => (Color.FromArgb(48, 20, 25), DangerPressed, Text),
            UiButtonKind.Danger when isHovered => (Color.FromArgb(67, 29, 35), Danger, Text),
            UiButtonKind.Danger => (Color.FromArgb(55, 24, 29), Danger, Text),
            UiButtonKind.Ghost when isPressed => (SurfacePressed, Border, Text),
            UiButtonKind.Ghost when isHovered => (SurfaceHovered, BorderHovered, Text),
            UiButtonKind.Ghost => (Surface, Border, Text),
            UiButtonKind.Subtle when isPressed => (SurfacePressed, SurfacePressed, Text),
            UiButtonKind.Subtle when isHovered => (SurfaceHovered, SurfaceHovered, Text),
            UiButtonKind.Subtle => (Surface, Surface, MutedText),
            _ when isPressed => (SurfacePressed, Border, Text),
            _ when isHovered => (SurfaceHovered, BorderHovered, Text),
            _ => (SurfaceRaised, Border, Text)
        };
    }

    public static void StyleCheckBox(CheckBox checkBox)
    {
        checkBox.BackColor = Color.Transparent;
        checkBox.ForeColor = Text;
        checkBox.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
    }

    /// <summary>
    /// Моноширинный шрифт с fallback: Cascadia Mono есть не на всех системах,
    /// Consolas — на любой Windows.
    /// </summary>
    public static Font CreateMonoFont(float size, FontStyle style = FontStyle.Regular)
    {
        foreach (var family in new[] { "Cascadia Mono", "Consolas" })
        {
            using var test = new Font(family, size, style, GraphicsUnit.Point);
            if (test.Name.Equals(family, StringComparison.OrdinalIgnoreCase))
            {
                return new Font(family, size, style, GraphicsUnit.Point);
            }
        }

        return new Font(FontFamily.GenericMonospace, size, style, GraphicsUnit.Point);
    }

    public static void StyleProgressBar(ProgressBar progressBar)
    {
        progressBar.BackColor = SurfaceRaised;
        progressBar.ForeColor = Accent;
        progressBar.Style = ProgressBarStyle.Continuous;
    }

    public static void StyleMenu(ContextMenuStrip menu)
    {
        menu.BackColor = Surface;
        menu.ForeColor = Text;
        menu.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColorTable());

        foreach (ToolStripItem item in menu.Items)
        {
            StyleMenuItem(item);
        }
    }

    private static void StyleMenuItem(ToolStripItem item)
    {
        item.BackColor = Surface;
        item.ForeColor = item.Enabled ? Text : MutedText;

        if (item is ToolStripMenuItem menuItem)
        {
            foreach (ToolStripItem child in menuItem.DropDownItems)
            {
                StyleMenuItem(child);
            }
        }
    }

    private sealed class DarkMenuColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Surface;
        public override Color ImageMarginGradientBegin => Surface;
        public override Color ImageMarginGradientMiddle => Surface;
        public override Color ImageMarginGradientEnd => Surface;
        public override Color MenuBorder => Border;
        public override Color MenuItemBorder => Border;
        public override Color MenuItemSelected => SurfaceRaised;
        public override Color MenuItemSelectedGradientBegin => SurfaceRaised;
        public override Color MenuItemSelectedGradientEnd => SurfaceRaised;
        public override Color MenuItemPressedGradientBegin => SurfaceHovered;
        public override Color MenuItemPressedGradientEnd => SurfaceHovered;
        public override Color SeparatorDark => Border;
        public override Color SeparatorLight => Border;
    }

    private static void ApplyDarkTitleBar(Form form)
    {
        if (!OperatingSystem.IsWindows() || form.IsDisposed || !form.IsHandleCreated)
        {
            return;
        }

        try
        {
            var useDarkMode = 1;
            var result = DwmSetWindowAttribute(form.Handle, 20, ref useDarkMode, sizeof(int));
            if (result != 0)
            {
                DwmSetWindowAttribute(form.Handle, 19, ref useDarkMode, sizeof(int));
            }

            var captionColor = ToColorRef(WindowBack);
            var textColor = ToColorRef(Text);
            var borderColor = ToColorRef(Border);
            DwmSetWindowAttribute(form.Handle, 35, ref captionColor, sizeof(int));
            DwmSetWindowAttribute(form.Handle, 36, ref textColor, sizeof(int));
            DwmSetWindowAttribute(form.Handle, 34, ref borderColor, sizeof(int));
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    private static int ToColorRef(Color color)
    {
        return color.R | (color.G << 8) | (color.B << 16);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);
}
