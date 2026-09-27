using System.Text.RegularExpressions;
using ZapretManager.App.Core;

namespace ZapretManager.App.Services;

public sealed record ManagerUpdateCheck(
    bool IsAvailable,
    string CurrentVersion,
    string LatestVersion,
    ReleaseAssetInfo? Asset);

/// <summary>
/// Обновление самого Zapret Manager из релизов его репозитория. Новый exe скачивается рядом,
/// проверяется по обязательному SHA-256 из релиза и подменяет текущий через <see cref="ExecutableReplacer"/>.
/// </summary>
public sealed class ManagerUpdateService
{
    public const string LatestReleaseUrl = "https://api.github.com/repos/elcapue/zapret-manager/releases/latest";

    // Имя ассета задаёт workflow релиза: ZapretManager-<версия>-win-x64.exe.
    private static readonly Regex AssetNamePattern = new(
        "^ZapretManager-[0-9A-Za-z.\\-]+-win-x64\\.exe$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly HttpClient _httpClient;

    public ManagerUpdateService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public static string CurrentVersion =>
        typeof(ManagerUpdateService).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    public async Task<ManagerUpdateCheck> CheckAsync(CancellationToken cancellationToken)
    {
        var release = await new GitHubReleaseClient(_httpClient, LatestReleaseUrl)
            .GetLatestReleaseAsync(cancellationToken)
            .ConfigureAwait(false);
        return Evaluate(release, CurrentVersion);
    }

    internal static ManagerUpdateCheck Evaluate(GitHubReleaseInfo release, string currentVersion)
    {
        var latestVersion = release.TagName.Trim().TrimStart('v', 'V');
        var asset = release.Assets.FirstOrDefault(candidate => AssetNamePattern.IsMatch(candidate.Name));
        var isAvailable = asset is not null && UpdateCheckService.IsNewer(latestVersion, currentVersion);
        return new ManagerUpdateCheck(isAvailable, currentVersion, latestVersion, asset);
    }

    /// <summary>Скачивает новый exe во временную папку. Без совпадающего SHA-256 файл не сохраняется.</summary>
    public async Task<string> DownloadAsync(ReleaseAssetInfo asset, string temporaryDirectory, CancellationToken cancellationToken)
    {
        var bytes = await _httpClient.GetByteArrayAsync(asset.DownloadUrl, cancellationToken).ConfigureAwait(false);
        if (!ReleaseDigest.Matches(bytes, asset.Digest, required: true))
        {
            throw new InvalidDataException("Контрольная сумма скачанного файла не совпала с релизом — обновление отменено.");
        }

        Directory.CreateDirectory(temporaryDirectory);
        var path = Path.Combine(temporaryDirectory, "zapret-manager-update-" + Guid.NewGuid().ToString("N") + ".exe");
        await File.WriteAllBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
        return path;
    }
}
