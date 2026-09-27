using System.Diagnostics;

namespace ZapretManager.App.Services;

public sealed class WinwsProcessInspector : IWinwsProcessInspector
{
    public IReadOnlyList<WinwsProcessInfo> FindRunning()
    {
        var processes = new List<WinwsProcessInfo>();
        foreach (var process in Process.GetProcessesByName("winws"))
        {
            using (process)
            {
                DateTime startedAtUtc;
                try
                {
                    startedAtUtc = process.StartTime.ToUniversalTime();
                }
                catch (InvalidOperationException)
                {
                    // The process exited while its identity was being inspected.
                    continue;
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // Windows did not expose enough identity data to compare this process safely.
                    continue;
                }

                var executablePath = string.Empty;
                try
                {
                    var inspectedPath = process.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(inspectedPath))
                    {
                        executablePath = Path.GetFullPath(inspectedPath);
                    }
                }
                catch (InvalidOperationException)
                {
                    continue;
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // Windows can temporarily deny MainModule access while the process is starting or exiting.
                    // PID plus exact start time still lets ProcessSupervisor reconcile the saved process safely.
                }

                processes.Add(new WinwsProcessInfo(process.Id, startedAtUtc, executablePath));
            }
        }

        return processes;
    }

    public bool TryStop(WinwsProcessInfo processInfo, out string errorMessage)
    {
        try
        {
            using var process = Process.GetProcessById(processInfo.ProcessId);
            if (process.HasExited)
            {
                errorMessage = string.Empty;
                return true;
            }

            if (process.StartTime.ToUniversalTime() != processInfo.StartedAtUtc)
            {
                errorMessage = "PID уже принадлежит другому процессу.";
                return false;
            }

            var executablePath = process.MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(executablePath) ||
                !PathComparer.AreEqual(executablePath, processInfo.ExecutablePath))
            {
                errorMessage = "Путь процесса не совпадает с сохранённым runtime.";
                return false;
            }

            process.Kill(entireProcessTree: true);
            if (!process.WaitForExit(10_000))
            {
                errorMessage = "Процесс не завершился за 10 секунд.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }
        catch (ArgumentException)
        {
            errorMessage = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }
}
