using ZapretManager.App.Services;
using ZapretManager.App.UI;

namespace ZapretManager.Tests;

public sealed class ApplicationLaunchOptionsTests
{
    [Theory]
    [InlineData("--autostart", true)]
    [InlineData("--AUTOSTART", true)]
    [InlineData("--other", false)]
    public void Parse_DetectsAutostartMode(string argument, bool expected)
    {
        Assert.Equal(expected, ApplicationLaunchOptions.Parse([argument]).IsAutostart);
    }

    [Fact]
    public void Parse_ReadsInstallRelatedFlagsIndependently()
    {
        var options = ApplicationLaunchOptions.Parse(["--no-install", "--FIRST-RUN", "--elevation-handoff"]);

        Assert.True(options.SkipInstall);
        Assert.True(options.IsFirstRun);
        Assert.False(options.IsUninstall);
        Assert.False(options.IsAutostart);
        Assert.True(ApplicationLaunchOptions.Parse(["--uninstall"]).IsUninstall);
    }

    [Theory]
    [InlineData(0, 0, 0, "00:00")]
    [InlineData(0, 8, 28, "08:28")]
    [InlineData(0, 137, 9, "2:17:09")]
    [InlineData(1, 125, 58, "1 д 02:05")]
    public void FormatUptime_IsCompactAndGrowsWithUptime(int days, int minutes, int seconds, string expected)
    {
        Assert.Equal(
            expected,
            TrayApplicationContext.FormatUptime(
                TimeSpan.FromDays(days) + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds)));
    }
}
