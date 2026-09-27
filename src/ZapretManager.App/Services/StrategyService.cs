using System.Text.RegularExpressions;
using ZapretManager.App.Core;

namespace ZapretManager.App.Services;

public static class StrategyService
{
    public static IReadOnlyList<StrategyInfo> DiscoverStrategies(string runtimeDirectory, string? selectedStrategy = null)
    {
        if (!Directory.Exists(runtimeDirectory))
        {
            return Array.Empty<StrategyInfo>();
        }

        return Directory.GetFiles(runtimeDirectory, "general*.bat")
            .Select(path => new StrategyInfo(
                Path.GetFileName(path),
                path,
                string.Equals(Path.GetFileName(path), selectedStrategy, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(strategy => GetSortKey(strategy.FileName), StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string GetSortKey(string fileName)
    {
        if (string.Equals(fileName, "general.bat", StringComparison.OrdinalIgnoreCase))
        {
            return "general";
        }

        return Regex.Replace(fileName, "\\d+", match => match.Value.PadLeft(8, '0'));
    }
}
