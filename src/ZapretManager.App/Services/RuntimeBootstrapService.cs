using ZapretManager.App.Core;

namespace ZapretManager.App.Services;

public sealed class RuntimeBootstrapService
{
    private readonly RuntimeLayout _layout;

    public RuntimeBootstrapService(RuntimeLayout layout)
    {
        _layout = layout;
    }

    public bool ShouldOfferBootstrap()
    {
        return !RuntimeValidator.Validate(_layout.RuntimeDirectory).IsComplete;
    }

    public async Task<UpdateApplyResult> InstallLatestAsync(HttpClient httpClient, AppConfig config, CancellationToken cancellationToken)
    {
        var releaseClient = new GitHubReleaseClient(httpClient);
        var release = await releaseClient.GetLatestReleaseAsync(cancellationToken).ConfigureAwait(false);
        var updateService = new UpdateService(httpClient, _layout);
        return await updateService.ApplyUpdateAsync(release, config, cancellationToken).ConfigureAwait(false);
    }
}
