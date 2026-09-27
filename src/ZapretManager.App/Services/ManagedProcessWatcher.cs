using System.ComponentModel;
using System.Diagnostics;
using ZapretManager.App.Core;

namespace ZapretManager.App.Services;

public static class ManagedProcessWatcher
{
    /// <summary>
    /// Ждёт завершения управляемого winws.exe. Возвращает true, когда процесс завершился
    /// (или его уже нет), и false, если следить за ним невозможно.
    /// </summary>
    public static Task<bool> WaitForExitAsync(ManagedProcessState state, CancellationToken cancellationToken)
    {
        return WaitForExitAsync(state.ProcessId, state.StartedAtUtc, cancellationToken);
    }

    /// <summary>Ждёт завершения любого из процессов (например, внешних winws.exe).</summary>
    public static async Task<bool> WaitForAnyExitAsync(IReadOnlyList<WinwsProcessInfo> processes, CancellationToken cancellationToken)
    {
        if (processes.Count == 0)
        {
            return true;
        }

        var first = await Task.WhenAny(processes.Select(process =>
            WaitForExitAsync(process.ProcessId, process.StartedAtUtc, cancellationToken))).ConfigureAwait(false);
        return await first.ConfigureAwait(false);
    }

    private static async Task<bool> WaitForExitAsync(int processId, DateTime startedAtUtc, CancellationToken cancellationToken)
    {
        Process process;
        try
        {
            process = Process.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
            return true;
        }

        using (process)
        {
            try
            {
                // PID мог достаться другому процессу — тогда наш уже завершён.
                if (process.StartTime.ToUniversalTime() != startedAtUtc)
                {
                    return true;
                }

                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
            catch (Win32Exception)
            {
                return false;
            }
        }
    }
}
