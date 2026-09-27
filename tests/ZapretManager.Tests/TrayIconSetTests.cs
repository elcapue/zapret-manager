using System.Drawing;
using ZapretManager.App.Core;
using ZapretManager.App.UI;

namespace ZapretManager.Tests;

public sealed class TrayIconSetTests
{
    [Fact]
    public void Get_UsesColorIconOnlyWhileBypassWorks()
    {
        using var icons = new TrayIconSet();

        Assert.Same(icons.Get(ZapretState.Stopped), icons.Get(ZapretState.RuntimeMissing));
        Assert.NotSame(icons.Get(ZapretState.Running), icons.Get(ZapretState.Stopped));
        Assert.NotSame(icons.Get(ZapretState.Running), icons.Get(ZapretState.External));
    }

    [Fact]
    public void Get_InactiveIconIsGrayscale()
    {
        using var icons = new TrayIconSet();
        using var inactive = icons.Get(ZapretState.Stopped).ToBitmap();
        using var running = icons.Get(ZapretState.Running).ToBitmap();

        Assert.True(HasColoredPixels(running));
        Assert.False(HasColoredPixels(inactive));
    }

    [Theory]
    [InlineData(ZapretState.Running, 124, 255, 74)]
    [InlineData(ZapretState.External, 255, 196, 70)]
    public void Get_ActiveStatesHaveBadgeInStatusColor(ZapretState state, int r, int g, int b)
    {
        using var icons = new TrayIconSet();
        using var bitmap = icons.Get(state).ToBitmap();

        // Метка — в правом нижнем углу.
        var badge = bitmap.GetPixel(bitmap.Width * 4 / 5, bitmap.Height * 4 / 5);

        Assert.Equal(Color.FromArgb(255, r, g, b), badge);
    }

    private static bool HasColoredPixels(Bitmap bitmap)
    {
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.A > 0 && (Math.Abs(pixel.R - pixel.G) > 8 || Math.Abs(pixel.G - pixel.B) > 8))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
