using System.Diagnostics;
using ZapretManager.App.Core;

namespace ZapretManager.App.Services;

public sealed class ZapretDetectionService
{
    private readonly IServiceStateReader _services;
    private readonly Func<bool> _isWinwsRunning;

    public ZapretDetectionService()
        : this(new WindowsServiceStateReader(), IsWinwsProcessRunning)
    {
    }

    internal ZapretDetectionService(IServiceStateReader services, Func<bool> isWinwsRunning)
    {
        _services = services;
        _isWinwsRunning = isWinwsRunning;
    }

    public ZapretStatus Detect()
    {
        var zapret = _services.Query("zapret");

        return new ZapretStatus
        {
            ZapretServiceExists = zapret != ServiceState.Missing,
            ZapretServiceRunning = zapret == ServiceState.Running,
            WinwsProcessRunning = _isWinwsRunning()
        };
    }

    private static bool IsWinwsProcessRunning()
    {
        var processes = Process.GetProcessesByName("winws");
        foreach (var process in processes)
        {
            process.Dispose();
        }

        return processes.Length > 0;
    }
}
