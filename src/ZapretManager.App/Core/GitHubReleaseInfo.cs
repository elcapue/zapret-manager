namespace ZapretManager.App.Core;

public sealed class GitHubReleaseInfo
{
    public GitHubReleaseInfo(
        string tagName,
        string releasePageUrl,
        ReleaseAssetInfo? zipAsset,
        IReadOnlyList<ReleaseAssetInfo>? assets = null)
    {
        TagName = tagName;
        ReleasePageUrl = releasePageUrl;
        ZipAsset = zipAsset;
        Assets = assets ?? (zipAsset is null ? [] : [zipAsset]);
    }

    public string TagName { get; }

    public string ReleasePageUrl { get; }

    public ReleaseAssetInfo? ZipAsset { get; }

    public IReadOnlyList<ReleaseAssetInfo> Assets { get; }
}
