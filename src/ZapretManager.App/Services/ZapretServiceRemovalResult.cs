namespace ZapretManager.App.Services;

public enum ZapretServiceRemovalStatus
{
    Removed,
    NotInstalled,
    CommandFailed
}

public sealed record ZapretServiceRemovalResult(ZapretServiceRemovalStatus Status, string Message)
{
    public bool IsSuccess => Status is ZapretServiceRemovalStatus.Removed or ZapretServiceRemovalStatus.NotInstalled;
}
