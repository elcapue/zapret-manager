namespace ZapretManager.App.Services;

public enum StrategyTargetGroup
{
    Baseline,
    Discord,
    YouTube
}

public sealed record StrategyTestTarget(string Name, Uri Url, StrategyTargetGroup Group = StrategyTargetGroup.Baseline)
{
    public string Host => Url.IdnHost;

    /// <summary>
    /// Возвращает null для строк, которые нельзя проверить HTTP-запросом
    /// (например, PING-цели из Flowseal targets.txt: ICMP ничего не говорит о DPI-обходе).
    /// </summary>
    public static StrategyTestTarget? TryCreate(
        string name,
        string value,
        StrategyTargetGroup group = StrategyTargetGroup.Baseline)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var url) ||
            (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp))
        {
            return null;
        }

        return new StrategyTestTarget(name, url, group);
    }

    public static IReadOnlyList<StrategyTestTarget> GetDefaultTargets()
    {
        return
        [
            Create("Discord Main", "https://discord.com", StrategyTargetGroup.Discord),
            Create("Discord Gateway", "https://gateway.discord.gg", StrategyTargetGroup.Discord),
            Create("Discord CDN", "https://cdn.discordapp.com", StrategyTargetGroup.Discord),
            Create("Discord Updates", "https://updates.discord.com", StrategyTargetGroup.Discord),
            Create("YouTube Web", "https://www.youtube.com", StrategyTargetGroup.YouTube),
            Create("YouTube Short", "https://youtu.be", StrategyTargetGroup.YouTube),
            Create("YouTube Image", "https://i.ytimg.com", StrategyTargetGroup.YouTube),
            Create("YouTube Video Redirect", "https://redirector.googlevideo.com", StrategyTargetGroup.YouTube),
            Create("Google Main", "https://www.google.com"),
            Create("Google Gstatic", "https://www.gstatic.com"),
            Create("Cloudflare Web", "https://www.cloudflare.com"),
            Create("Cloudflare CDN", "https://cdnjs.cloudflare.com")
        ];
    }

    private static StrategyTestTarget Create(string name, string url, StrategyTargetGroup group = StrategyTargetGroup.Baseline)
    {
        return new StrategyTestTarget(name, new Uri(url), group);
    }
}
