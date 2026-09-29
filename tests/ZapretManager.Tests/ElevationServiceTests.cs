using System.ComponentModel;
using ZapretManager.App.Services;

namespace ZapretManager.Tests;

public sealed class ElevationServiceTests
{
    [Fact]
    public void IsElevationDeclinedByUser_WhenUacCancelled_ReturnsTrue()
    {
        Assert.True(ElevationService.IsElevationDeclinedByUser(new Win32Exception(1223)));
    }

    [Fact]
    public void IsElevationDeclinedByUser_WhenOtherWin32Error_ReturnsFalse()
    {
        Assert.False(ElevationService.IsElevationDeclinedByUser(new Win32Exception(5))); // ERROR_ACCESS_DENIED
    }

    [Fact]
    public void IsElevationDeclinedByUser_WhenNotWin32Exception_ReturnsFalse()
    {
        Assert.False(ElevationService.IsElevationDeclinedByUser(new IOException("disk full")));
    }
}
