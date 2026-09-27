namespace ZapretManager.App.Services;

public enum ZapretActionKind
{
    Start,
    Stop,
    Restart,
    StopExisting,
    AutoSelect
}

public enum ZapretActionOutcome
{
    Succeeded,
    Failed,
    Cancelled
}

public sealed record ZapretActionResponse(
    ZapretActionOutcome Outcome,
    string Message,
    StrategyAutoSelectionResult? AutoSelection = null)
{
    public bool IsSuccess => Outcome == ZapretActionOutcome.Succeeded;
}
