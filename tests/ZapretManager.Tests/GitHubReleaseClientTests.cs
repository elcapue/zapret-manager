using System.Net;
using ZapretManager.App.Services;

namespace ZapretManager.Tests;

public sealed class GitHubReleaseClientTests
{
    [Fact]
    public async Task GetLatestReleaseAsync_ParsesTagAndZipAssetFromGitHubResponse()
    {
        const string responseJson = """
        {
          "tag_name": "1.10.2",
          "html_url": "https://github.com/Flowseal/zapret-discord-youtube/releases/tag/1.10.2",
          "assets": [
            {
              "name": "zapret-discord-youtube-1.10.2.zip",
              "browser_download_url": "https://github.com/Flowseal/zapret-discord-youtube/releases/download/1.10.2/zapret-discord-youtube-1.10.2.zip",
              "digest": "sha256:abc123"
            },
            {
              "name": "zapret-discord-youtube-1.10.2.rar",
              "browser_download_url": "https://example.test/file.rar"
            }
          ]
        }
        """;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(responseJson));
        var client = new GitHubReleaseClient(httpClient);

        var release = await client.GetLatestReleaseAsync(CancellationToken.None);

        Assert.Equal("1.10.2", release.TagName);
        Assert.Equal("https://github.com/Flowseal/zapret-discord-youtube/releases/tag/1.10.2", release.ReleasePageUrl);
        Assert.NotNull(release.ZipAsset);
        Assert.Equal("zapret-discord-youtube-1.10.2.zip", release.ZipAsset!.Name);
        Assert.Equal("sha256:abc123", release.ZipAsset.Digest);
        Assert.Equal(2, release.Assets.Count);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseJson;

        public StubHttpMessageHandler(string responseJson)
        {
            _responseJson = responseJson;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("https://api.github.com/repos/Flowseal/zapret-discord-youtube/releases/latest", request.RequestUri?.ToString());
            Assert.True(request.Headers.UserAgent.Count > 0);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseJson)
            };
            return Task.FromResult(response);
        }
    }
}
