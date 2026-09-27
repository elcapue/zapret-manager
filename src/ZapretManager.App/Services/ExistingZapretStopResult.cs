namespace ZapretManager.App.Services;

public enum ExistingZapretStopStatus
{
    Stopped,
    NothingToStop,
    CommandFailed
}

public sealed record ExistingZapretStopResult(ExistingZapretStopStatus Status, string Message)
{
    public bool IsSuccess => Status is ExistingZapretStopStatus.Stopped or ExistingZapretStopStatus.NothingToStop;
}
