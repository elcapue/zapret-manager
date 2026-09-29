namespace ZapretManager.App.Core;

public sealed class GitHubReleaseInfo
{
    public GitHubReleaseInfo(
        string tagName,
        ReleaseAssetInfo? zipAsset,
        IReadOnlyList<ReleaseAssetInfo>? assets = null)
    {
        TagName = tagName;
        ZipAsset = zipAsset;
        Assets = assets ?? (zipAsset is null ? [] : [zipAsset]);
    }

    public string TagName { get; }

    public ReleaseAssetInfo? ZipAsset { get; }

    public IReadOnlyList<ReleaseAssetInfo> Assets { get; }
}
