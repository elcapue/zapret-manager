namespace ZapretManager.App.Services;

public sealed record ManagedProcessShutdownResult(bool CanExit, string Message);

public sealed class ManagedProcessShutdownService
{
    private readonly Func<bool> _isManagedProcessRunning;
    private readonly Func<Task<ZapretActionResponse>> _stopManagedProcess;

    public ManagedProcessShutdownService(
        Func<bool> isManagedProcessRunning,
        Func<Task<ZapretActionResponse>> stopManagedProcess)
    {
        _isManagedProcessRunning = isManagedProcessRunning;
        _stopManagedProcess = stopManagedProcess;
    }

    public async Task<ManagedProcessShutdownResult> StopBeforeExitAsync()
    {
        if (!_isManagedProcessRunning())
        {
            return new ManagedProcessShutdownResult(true, string.Empty);
        }

        var response = await _stopManagedProcess().ConfigureAwait(true);
        if (response.IsSuccess)
        {
            return new ManagedProcessShutdownResult(true, response.Message);
        }

        if (!_isManagedProcessRunning())
        {
            return new ManagedProcessShutdownResult(true, response.Message);
        }

        return new ManagedProcessShutdownResult(false, response.Message);
    }
}
