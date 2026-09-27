namespace ZapretManager.App.Core;

public sealed class ReleaseAssetInfo
{
    public ReleaseAssetInfo(string name, string downloadUrl, string? digest)
    {
        Name = name;
        DownloadUrl = downloadUrl;
        Digest = digest;
    }

    public string Name { get; }

    public string DownloadUrl { get; }

    public string? Digest { get; }
}
