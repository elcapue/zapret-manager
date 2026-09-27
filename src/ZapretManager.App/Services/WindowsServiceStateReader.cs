using System.Runtime.InteropServices;

namespace ZapretManager.App.Services;

public enum ServiceState
{
    Missing,
    Stopped,
    Running
}

public interface IServiceStateReader
{
    ServiceState Query(string serviceName);
}

/// <summary>
/// Читает состояние службы напрямую через Service Control Manager, без запуска sc.exe.
/// Статус опрашивается из UI-потока, поэтому процесс на каждую службу здесь слишком дорог.
/// </summary>
public sealed class WindowsServiceStateReader : IServiceStateReader
{
    private const uint ScManagerConnect = 0x0001;
    private const uint ServiceQueryStatus = 0x0004;
    private const int ErrorServiceDoesNotExist = 1060;
    private const int ErrorAccessDenied = 5;
    private const uint ServiceRunning = 4;

    public ServiceState Query(string serviceName)
    {
        var manager = OpenSCManagerW(null, null, ScManagerConnect);
        if (manager == IntPtr.Zero)
        {
            return ServiceState.Missing;
        }

        try
        {
            var service = OpenServiceW(manager, serviceName, ServiceQueryStatus);
            if (service == IntPtr.Zero)
            {
                // Служба есть, но прав на опрос нет — считаем существующей и не запущенной.
                return Marshal.GetLastWin32Error() == ErrorAccessDenied
                    ? ServiceState.Stopped
                    : ServiceState.Missing;
            }

            try
            {
                return QueryServiceStatus(service, out var status) && status.CurrentState == ServiceRunning
                    ? ServiceState.Running
                    : ServiceState.Stopped;
            }
            finally
            {
                CloseServiceHandle(service);
            }
        }
        finally
        {
            CloseServiceHandle(manager);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceStatus
    {
        public uint ServiceType;
        public uint CurrentState;
        public uint ControlsAccepted;
        public uint Win32ExitCode;
        public uint ServiceSpecificExitCode;
        public uint CheckPoint;
        public uint WaitHint;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenSCManagerW(string? machineName, string? databaseName, uint desiredAccess);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenServiceW(IntPtr serviceManager, string serviceName, uint desiredAccess);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryServiceStatus(IntPtr service, out ServiceStatus serviceStatus);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseServiceHandle(IntPtr handle);
}
