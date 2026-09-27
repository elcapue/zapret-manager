namespace ZapretManager.App.Services;

public interface IWinwsProcessInspector
{
    IReadOnlyList<WinwsProcessInfo> FindRunning();

    bool TryStop(WinwsProcessInfo process, out string errorMessage);
}
