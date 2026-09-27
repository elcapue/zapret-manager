namespace ZapretManager.App.Services;

public sealed record StrategyAutoSelectionProgress(
    int CompletedStrategies,
    int TotalStrategies,
    string CurrentStrategyName,
    string Message);
