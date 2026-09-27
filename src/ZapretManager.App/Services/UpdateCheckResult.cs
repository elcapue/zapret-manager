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
        string releasePageUrl,
        ReleaseAssetInfo? zipAsset)
    {
        Availability = availability;
        CurrentVersion = currentVersion;
        LatestVersion = latestVersion;
        ReleasePageUrl = releasePageUrl;
        ZipAsset = zipAsset;
    }

    public UpdateAvailability Availability { get; }

    public string CurrentVersion { get; }

    public string LatestVersion { get; }

    public string ReleasePageUrl { get; }

    public ReleaseAssetInfo? ZipAsset { get; }
}
