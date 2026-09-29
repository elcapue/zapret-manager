using ZapretManager.App.Core;

namespace ZapretManager.App.Services;

public enum UpdateAvailability
{
    UpToDate,
    UpdateAvailable,
    UnknownCurrentVersion
}

public sealed class UpdateCheckResult
{
    public UpdateCheckResult(
        UpdateAvailability availability,
        string currentVersion,
        string latestVersion,
        ReleaseAssetInfo? zipAsset)
    {
        Availability = availability;
        CurrentVersion = currentVersion;
        LatestVersion = latestVersion;
        ZipAsset = zipAsset;
    }

    public UpdateAvailability Availability { get; }

    public string CurrentVersion { get; }

    public string LatestVersion { get; }

    public ReleaseAssetInfo? ZipAsset { get; }
}
