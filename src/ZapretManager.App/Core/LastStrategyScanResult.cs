namespace ZapretManager.App.Core;

public sealed class LastStrategyScanResult
{
    public DateTimeOffset ScannedAtUtc { get; set; }

    public List<LastStrategyScanSummary> Strategies { get; set; } = [];
}

public sealed class LastStrategyScanSummary
{
    public string StrategyFileName { get; set; } = string.Empty;

    public int HttpOk { get; set; }

    public int HttpError { get; set; }

    public int YouTubeHttpOk { get; set; }

    public int YouTubeHttpAttempts { get; set; }

    public int DiscordHttpOk { get; set; }

    public int DiscordHttpAttempts { get; set; }

    public int MedianLatencyMs { get; set; }
}
