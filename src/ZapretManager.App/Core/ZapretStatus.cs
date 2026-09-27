namespace ZapretManager.App.Core;

public sealed class ZapretStatus
{
    public bool ZapretServiceExists { get; init; }

    public bool ZapretServiceRunning { get; init; }

    public bool WinwsProcessRunning { get; init; }
}
