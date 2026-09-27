using ZapretManager.App.Core;

namespace ZapretManager.App.Services;

public static class StrategyAutoSelectionAnalyzer
{
    /// <summary>
    /// Порядок: запустилась → доля успешных проверок → баланс YouTube/Discord →
    /// текущая стратегия → медианная задержка. Имя — только для детерминизма при полном совпадении.
    /// </summary>
    /// <param name="preferred">
    /// Текущая стратегия пользователя. При равном качестве она выигрывает у остальных: разница
    /// в задержке в десяток миллисекунд — шум, а менять проверенную на практике стратегию на
    /// случайного «победителя» нельзя.
    /// </param>
    public static IReadOnlyList<StrategyTestSummary> Rank(
        IEnumerable<StrategyTestSummary> summaries,
        StrategyInfo? preferred = null)
    {
        return summaries
            .OrderBy(summary => summary.StartFailed)
            .ThenByDescending(summary => summary.SuccessRate)
            .ThenByDescending(summary => summary.ServiceBalance)
            .ThenByDescending(summary => IsSameStrategy(summary.Strategy, preferred))
            .ThenBy(summary => summary.MedianLatencyMs > 0 ? summary.MedianLatencyMs : int.MaxValue)
            .ThenBy(summary => summary.Strategy.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool IsSameStrategy(StrategyInfo strategy, StrategyInfo? other)
    {
        return other is not null && string.Equals(strategy.FileName, other.FileName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Стратегии неразличимы по качеству и отличаются только скоростью.</summary>
    public static bool HaveSameScore(StrategyTestSummary first, StrategyTestSummary second)
    {
        return first.StartFailed == second.StartFailed &&
               first.SuccessRate.Equals(second.SuccessRate) &&
               first.ServiceBalance.Equals(second.ServiceBalance);
    }
}
