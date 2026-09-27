using ZapretManager.App.Core;

namespace ZapretManager.App.Services;

/// <summary>
/// Итог проверки стратегии. Учитываются только попытки, отражающие работу DPI-обхода
/// (<see cref="ProbeAttempt.IsCounted"/>): сбои DNS и подмена сертификата в счёт не идут.
/// </summary>
public sealed record StrategyTestSummary(
    StrategyInfo Strategy,
    int HttpOk,
    int HttpError,
    int YouTubeHttpOk = 0,
    int YouTubeHttpAttempts = 0,
    int DiscordHttpOk = 0,
    int DiscordHttpAttempts = 0,
    int MedianLatencyMs = 0,
    bool StartFailed = false,
    int Rounds = 1)
{
    public int Attempts => HttpOk + HttpError;

    public double SuccessRate => Rate(HttpOk, Attempts);

    /// <summary>Худшая доля успеха среди YouTube и Discord: стратегия, открывающая оба сервиса, лучше «однобокой».</summary>
    public double ServiceBalance
    {
        get
        {
            var rates = new List<double>(2);
            if (YouTubeHttpAttempts > 0)
            {
                rates.Add(Rate(YouTubeHttpOk, YouTubeHttpAttempts));
            }

            if (DiscordHttpAttempts > 0)
            {
                rates.Add(Rate(DiscordHttpOk, DiscordHttpAttempts));
            }

            return rates.Count == 0 ? SuccessRate : rates.Min();
        }
    }

    public static StrategyTestSummary FromAttempts(StrategyInfo strategy, IEnumerable<ProbeAttempt> attempts, int rounds = 1)
    {
        var counted = attempts.Where(attempt => attempt.IsCounted).ToArray();
        var youTube = counted.Where(attempt => attempt.Target.Group == StrategyTargetGroup.YouTube).ToArray();
        var discord = counted.Where(attempt => attempt.Target.Group == StrategyTargetGroup.Discord).ToArray();
        var ok = counted.Count(IsOk);

        return new StrategyTestSummary(
            strategy,
            HttpOk: ok,
            HttpError: counted.Length - ok,
            YouTubeHttpOk: youTube.Count(IsOk),
            YouTubeHttpAttempts: youTube.Length,
            DiscordHttpOk: discord.Count(IsOk),
            DiscordHttpAttempts: discord.Length,
            MedianLatencyMs: MedianLatencyMilliseconds(counted),
            Rounds: rounds);
    }

    public static StrategyTestSummary NotStarted(StrategyInfo strategy)
    {
        return new StrategyTestSummary(strategy, HttpOk: 0, HttpError: 0, StartFailed: true);
    }

    private static bool IsOk(ProbeAttempt attempt) => attempt.Outcome == ProbeOutcome.Ok;

    private static double Rate(int ok, int attempts) => attempts == 0 ? 0 : (double)ok / attempts;

    private static int MedianLatencyMilliseconds(IEnumerable<ProbeAttempt> attempts)
    {
        var latencies = attempts
            .Where(IsOk)
            .Select(attempt => attempt.Latency.TotalMilliseconds)
            .Order()
            .ToArray();
        if (latencies.Length == 0)
        {
            return 0;
        }

        var middle = latencies.Length / 2;
        var median = latencies.Length % 2 == 1
            ? latencies[middle]
            : (latencies[middle - 1] + latencies[middle]) / 2;
        return Math.Max(1, (int)Math.Round(median));
    }
}
