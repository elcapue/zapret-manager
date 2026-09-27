using ZapretManager.App.Core;

namespace ZapretManager.App.Services;

public enum StrategyAutoSelectionStatus
{
    Completed,
    NoStrategies,
    NoWorkingStrategy,
    StartFailed,
    StopFailed
}

/// <param name="Summaries">Все проверенные стратегии, уже отсортированные от лучшей к худшей.</param>
/// <param name="Notes">Диагностика прогона: что заблокировано без zapret, проблемы DNS, ничьи.</param>
public sealed record StrategyAutoSelectionResult(
    StrategyAutoSelectionStatus Status,
    StrategyInfo? BestStrategy,
    IReadOnlyList<StrategyTestSummary> Summaries,
    string Message,
    IReadOnlyList<string>? Notes = null)
{
    public IReadOnlyList<string> Notes { get; init; } = Notes ?? Array.Empty<string>();

    public IReadOnlyList<StrategyTestSummary> TopStrategies =>
        Summaries.Where(summary => !summary.StartFailed).Take(3).ToArray();

    public bool IsSuccess => Status == StrategyAutoSelectionStatus.Completed;
}
