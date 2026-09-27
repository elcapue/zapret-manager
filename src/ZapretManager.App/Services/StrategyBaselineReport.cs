namespace ZapretManager.App.Services;

/// <summary>
/// Результат проверки целей без zapret: показывает, что реально заблокировано,
/// а что недоступно по причинам, которые стратегия не исправит.
/// </summary>
public sealed record StrategyBaselineReport(
    IReadOnlyList<StrategyTestTarget> Blocked,
    IReadOnlyList<StrategyTestTarget> Reachable,
    IReadOnlyList<StrategyTestTarget> DnsFailures,
    IReadOnlyList<StrategyTestTarget> CertificateErrors,
    double SuccessRate,
    IReadOnlyList<StrategyTestTarget>? ResolvedViaFallbackDns = null)
{
    /// <summary>Цели, которые DNS провайдера не нашёл, а DNS-over-HTTPS нашёл.</summary>
    public IReadOnlyList<StrategyTestTarget> ResolvedViaFallbackDns { get; init; } =
        ResolvedViaFallbackDns ?? Array.Empty<StrategyTestTarget>();

    public static StrategyBaselineReport From(IReadOnlyList<ProbeAttempt> attempts)
    {
        var blocked = new List<StrategyTestTarget>();
        var reachable = new List<StrategyTestTarget>();
        var dnsFailures = new List<StrategyTestTarget>();
        var certificateErrors = new List<StrategyTestTarget>();

        foreach (var group in attempts.GroupBy(attempt => attempt.Target))
        {
            if (group.Any(attempt => attempt.Outcome == ProbeOutcome.DnsFailure))
            {
                dnsFailures.Add(group.Key);
            }
            else if (group.Any(attempt => attempt.Outcome == ProbeOutcome.CertificateError))
            {
                certificateErrors.Add(group.Key);
            }
            else if (group.Any(attempt => attempt.Outcome == ProbeOutcome.Ok))
            {
                reachable.Add(group.Key);
            }
            else
            {
                blocked.Add(group.Key);
            }
        }

        var counted = attempts.Where(attempt => attempt.IsCounted).ToArray();
        var successRate = counted.Length == 0
            ? 0
            : (double)counted.Count(attempt => attempt.Outcome == ProbeOutcome.Ok) / counted.Length;
        var viaFallbackDns = attempts
            .Where(attempt => attempt.ResolvedViaFallbackDns)
            .Select(attempt => attempt.Target)
            .Distinct()
            .ToArray();
        return new StrategyBaselineReport(blocked, reachable, dnsFailures, certificateErrors, successRate, viaFallbackDns);
    }
}
