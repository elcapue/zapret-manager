namespace ZapretManager.App.Services;

public static class UpdateNotificationPolicy
{
    public static bool ShouldShowMessage(UpdateAvailability availability, bool isAutomaticStartupCheck)
    {
        return !(isAutomaticStartupCheck && availability == UpdateAvailability.UpToDate);
    }
}
