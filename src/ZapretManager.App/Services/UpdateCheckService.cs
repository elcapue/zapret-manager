using System.Text.RegularExpressions;
using ZapretManager.App.Core;

namespace ZapretManager.App.Services;

public static class UpdateCheckService
{
    // Части тегов Flowseal: число и необязательный буквенный хотфикс («1.9.7b» новее «1.9.7»).
    private static readonly Regex VersionPartPattern = new("^(\\d+)([a-z]*)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static UpdateCheckResult Compare(string? currentVersion, GitHubReleaseInfo release)
    {
        if (string.IsNullOrWhiteSpace(currentVersion))
        {
            return new UpdateCheckResult(
                UpdateAvailability.UnknownCurrentVersion,
                "неизвестна",
                release.TagName,
                release.ZipAsset);
        }

        var availability = IsNewer(release.TagName, currentVersion)
            ? UpdateAvailability.UpdateAvailable
            : UpdateAvailability.UpToDate;

        return new UpdateCheckResult(
            availability,
            currentVersion,
            release.TagName,
            release.ZipAsset);
    }

    /// <summary>
    /// Установленная версия новее последнего релиза (релиз отозвали) — это не обновление.
    /// Непохожий на версию тег сравнивается как строка: отличается — значит, обновление.
    /// </summary>
    public static bool IsNewer(string latest, string current)
    {
        var latestParts = Parse(latest);
        var currentParts = Parse(current);
        if (latestParts is null || currentParts is null)
        {
            return !string.Equals(Normalize(latest), Normalize(current), StringComparison.OrdinalIgnoreCase);
        }

        for (var index = 0; index < Math.Max(latestParts.Count, currentParts.Count); index++)
        {
            var latestPart = index < latestParts.Count ? latestParts[index] : (0, string.Empty);
            var currentPart = index < currentParts.Count ? currentParts[index] : (0, string.Empty);
            var byNumber = latestPart.Number.CompareTo(currentPart.Number);
            if (byNumber != 0)
            {
                return byNumber > 0;
            }

            var bySuffix = string.Compare(latestPart.Suffix, currentPart.Suffix, StringComparison.OrdinalIgnoreCase);
            if (bySuffix != 0)
            {
                return bySuffix > 0;
            }
        }

        return false;
    }

    private static IReadOnlyList<(long Number, string Suffix)>? Parse(string version)
    {
        var parts = new List<(long Number, string Suffix)>();
        foreach (var part in Normalize(version).Split('.'))
        {
            var match = VersionPartPattern.Match(part);
            if (!match.Success || !long.TryParse(match.Groups[1].Value, out var number))
            {
                return null;
            }

            parts.Add((number, match.Groups[2].Value));
        }

        return parts;
    }

    private static string Normalize(string version)
    {
        return version.Trim().TrimStart('v', 'V');
    }
}
