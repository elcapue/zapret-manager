using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using ZapretManager.App.Core;

namespace ZapretManager.App.Services;

public sealed class GitHubReleaseClient
{
    public const string FlowsealLatestReleaseUrl = "https://api.github.com/repos/Flowseal/zapret-discord-youtube/releases/latest";

    private readonly HttpClient _httpClient;
    private readonly string _latestReleaseUrl;

    public GitHubReleaseClient(HttpClient httpClient, string latestReleaseUrl = FlowsealLatestReleaseUrl)
    {
        _httpClient = httpClient;
        _latestReleaseUrl = latestReleaseUrl;
    }

    public async Task<GitHubReleaseInfo> GetLatestReleaseAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _latestReleaseUrl);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("ZapretManager", typeof(GitHubReleaseClient).Assembly.GetName().Version?.ToString(3) ?? "0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var dto = await JsonSerializer.DeserializeAsync<GitHubReleaseDto>(stream, cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("GitHub release response is empty.");

        var assets = dto.Assets
            .Select(asset => new ReleaseAssetInfo(asset.Name, asset.DownloadUrl, asset.Digest))
            .ToArray();
        var zipAsset = assets.FirstOrDefault(asset => asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

        return new GitHubReleaseInfo(dto.TagName, dto.HtmlUrl, zipAsset, assets);
    }

    private sealed class GitHubReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        [JsonPropertyName("assets")]
        public List<GitHubReleaseAssetDto> Assets { get; set; } = [];
    }

    private sealed class GitHubReleaseAssetDto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string DownloadUrl { get; set; } = string.Empty;

        [JsonPropertyName("digest")]
        public string? Digest { get; set; }
    }
}
