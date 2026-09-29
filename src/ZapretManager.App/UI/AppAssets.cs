using System.Reflection;
using System.Runtime.InteropServices;

namespace ZapretManager.App.UI;

public static class AppAssets
{
    public const string IconFileName = "zapret-manager.ico";
    public const string TrayIconFileName = "zapret-manager-tray-32.png";
    public const string MarkFileName = "zapret-manager-mark-128.png";

    public static string GetBrandingResourceName(string fileName)
    {
        return "ZapretManager.App.Assets.Branding." + fileName;
    }

    public static Icon LoadWindowIcon()
    {
        return LoadIconFromResourceOrFallback(IconFileName, size: null);
    }

    public static Icon LoadTrayIcon()
    {
        return LoadIconFromResourceOrFallback(IconFileName, size: 32)
            ?? LoadPngAsIconOrFallback(TrayIconFileName, 32);
    }

    public static Bitmap? LoadMark()
    {
        using var stream = OpenBrandingResource(MarkFileName);
        if (stream is null)
        {
            return null;
        }

        using var memory = CopyToMemoryStream(stream);
        using var image = Image.FromStream(memory);
        return new Bitmap(image);
    }

    private static Icon LoadIconFromResourceOrFallback(string fileName, int? size)
    {
        using var stream = OpenBrandingResource(fileName);
        if (stream is null)
        {
            return SystemIcons.Application;
        }

        using var memory = CopyToMemoryStream(stream);
        return size is null ? new Icon(memory) : new Icon(memory, size.Value, size.Value);
    }

    private static Icon LoadPngAsIconOrFallback(string fileName, int size)
    {
        using var stream = OpenBrandingResource(fileName);
        if (stream is null)
        {
            return SystemIcons.Application;
        }

        using var bitmap = new Bitmap(stream);
        using var resized = new Bitmap(bitmap, new Size(size, size));
        var handle = resized.GetHicon();
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

    private static Stream? OpenBrandingResource(string fileName)
    {
        return Assembly.GetExecutingAssembly().GetManifestResourceStream(GetBrandingResourceName(fileName));
    }

    private static MemoryStream CopyToMemoryStream(Stream stream)
    {
        var memory = new MemoryStream();
        stream.CopyTo(memory);
        memory.Position = 0;
        return memory;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
