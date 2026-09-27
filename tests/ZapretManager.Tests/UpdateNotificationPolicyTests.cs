using ZapretManager.App.Services;

namespace ZapretManager.Tests;

public sealed class UpdateNotificationPolicyTests
{
    [Fact]
    public void ShouldShowMessage_WhenAutomaticCheckAndUpToDate_ReturnsFalse()
    {
        Assert.False(UpdateNotificationPolicy.ShouldShowMessage(UpdateAvailability.UpToDate, isAutomaticStartupCheck: true));
    }

    [Fact]
    public void ShouldShowMessage_WhenManualCheckAndUpToDate_ReturnsTrue()
    {
        Assert.True(UpdateNotificationPolicy.ShouldShowMessage(UpdateAvailability.UpToDate, isAutomaticStartupCheck: false));
    }

    [Theory]
    [InlineData(UpdateAvailability.UpdateAvailable)]
    [InlineData(UpdateAvailability.UnknownCurrentVersion)]
    public void ShouldShowMessage_WhenAutomaticCheckNeedsAttention_ReturnsTrue(UpdateAvailability availability)
    {
        Assert.True(UpdateNotificationPolicy.ShouldShowMessage(availability, isAutomaticStartupCheck: true));
    }
}
