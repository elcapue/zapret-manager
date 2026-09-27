using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ZapretManager.App.Core;

namespace ZapretManager.App.UI;

/// <summary>
/// Иконки трея по состоянию zapret: цветная с зелёной меткой — обход работает (свой zapret),
/// цветная с жёлтой меткой — работает внешний zapret, серая — выключено или нет runtime.
/// Строятся один раз из фирменной иконки и живут до выхода.
/// </summary>
public sealed class TrayIconSet : IDisposable
{
    private readonly Icon _running;
    private readonly Icon _external;
    private readonly Icon _inactive;

    public TrayIconSet()
    {
        // Иконку-источник не освобождаем: при отсутствии ресурса это общий SystemIcons.Application.
        var source = AppAssets.LoadTrayIcon();
        using var sourceBitmap = source.ToBitmap();
        using var runningBitmap = WithBadge(sourceBitmap, UiTheme.StatusRunning);
        _running = ToOwnedIcon(runningBitmap);

        using var externalBitmap = WithBadge(sourceBitmap, UiTheme.StatusExternal);
        _external = ToOwnedIcon(externalBitmap);

        using var inactiveBitmap = ToGrayscale(sourceBitmap);
        _inactive = ToOwnedIcon(inactiveBitmap);
    }

    public Icon Get(ZapretState state)
    {
        return state switch
        {
            ZapretState.Running => _running,
            ZapretState.External => _external,
            _ => _inactive
        };
    }

    public void Dispose()
    {
        _running.Dispose();
        _external.Dispose();
        _inactive.Dispose();
    }

    private static Bitmap ToGrayscale(Bitmap source)
    {
        var result = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        var matrix = new ColorMatrix(
        [
            [0.30F, 0.30F, 0.30F, 0F, 0F],
            [0.59F, 0.59F, 0.59F, 0F, 0F],
            [0.11F, 0.11F, 0.11F, 0F, 0F],
            [0F, 0F, 0F, 1F, 0F],
            [0F, 0F, 0F, 0F, 1F]
        ]);
        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(matrix);
        using var graphics = Graphics.FromImage(result);
        graphics.DrawImage(
            source,
            new Rectangle(0, 0, source.Width, source.Height),
            0,
            0,
            source.Width,
            source.Height,
            GraphicsUnit.Pixel,
            attributes);
        return result;
    }

    private static Bitmap WithBadge(Bitmap source, Color color)
    {
        var result = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(result);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.DrawImage(source, 0, 0, source.Width, source.Height);

        var diameter = source.Width * 0.42F;
        var bounds = new RectangleF(source.Width - diameter - 0.5F, source.Height - diameter - 0.5F, diameter, diameter);
        using var fill = new SolidBrush(color);
        using var outline = new Pen(UiTheme.WindowBack, Math.Max(1.5F, source.Width / 12F));
        graphics.FillEllipse(fill, bounds);
        graphics.DrawEllipse(outline, bounds);
        return result;
    }

    private static Icon ToOwnedIcon(Bitmap bitmap)
    {
        var handle = bitmap.GetHicon();
        try
        {
            using var borrowed = Icon.FromHandle(handle);
            // Clone копирует HICON, поэтому исходный handle можно сразу освободить.
            return (Icon)borrowed.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
