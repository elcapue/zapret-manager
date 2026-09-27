namespace ZapretManager.App.Services;

public enum ZapretRunStatus
{
    Started,
    Stopped,
    Restarted,
    NoStrategySelected,
    AlreadyRunningManaged,
    AlreadyRunningNotOwned,
    NotStartedByManager,
    CommandFailed
}

public sealed record ZapretRunResult(ZapretRunStatus Status, string Message)
{
    public bool IsSuccess => Status is
        ZapretRunStatus.Started or
        ZapretRunStatus.Stopped or
        ZapretRunStatus.Restarted or
        ZapretRunStatus.NotStartedByManager;
}
