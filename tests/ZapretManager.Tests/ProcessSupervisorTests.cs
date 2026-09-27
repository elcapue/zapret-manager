using ZapretManager.App.Core;
using ZapretManager.App.Services;

namespace ZapretManager.Tests;

public sealed class ProcessSupervisorTests
{
    [Fact]
    public void ReconcileManagedProcess_WhenNonElevatedInspectorCannotReadPath_KeepsManagedIdentity()
    {
        var startedAtUtc = new DateTime(2026, 8, 27, 17, 0, 0, DateTimeKind.Utc);
        var config = new AppConfig
        {
            ManagedProcess = new ManagedProcessState
            {
                ProcessId = 1234,
                StartedAtUtc = startedAtUtc,
                ExecutablePath = "D:/Zapret/runtime/bin/winws.exe",
                StrategyFileName = "general.bat"
            }
        };
        var inspector = new FakeWinwsProcessInspector(
            new WinwsProcessInfo(1234, startedAtUtc, string.Empty));
        var supervisor = new ProcessSupervisor(inspector, config);

        var process = supervisor.ReconcileManagedProcess();

        Assert.NotNull(process);
        Assert.Equal(1234, process.ProcessId);
        Assert.NotNull(config.ManagedProcess);
    }

    [Fact]
    public void ReconcileManagedProcess_WhenStartTimeDoesNotMatch_DoesNotTrustMissingPath()
    {
        var startedAtUtc = new DateTime(2026, 8, 27, 17, 0, 0, DateTimeKind.Utc);
        var config = new AppConfig
        {
            ManagedProcess = new ManagedProcessState
            {
                ProcessId = 1234,
                StartedAtUtc = startedAtUtc,
                ExecutablePath = "D:/Zapret/runtime/bin/winws.exe",
                StrategyFileName = "general.bat"
            }
        };
        var inspector = new FakeWinwsProcessInspector(
            new WinwsProcessInfo(1234, startedAtUtc.AddSeconds(1), string.Empty));
        var supervisor = new ProcessSupervisor(inspector, config);

        var process = supervisor.ReconcileManagedProcess();

        Assert.Null(process);
        Assert.Null(config.ManagedProcess);
    }

    private sealed class FakeWinwsProcessInspector : IWinwsProcessInspector
    {
        private readonly WinwsProcessInfo[] _processes;

        public FakeWinwsProcessInspector(params WinwsProcessInfo[] processes)
        {
            _processes = processes;
        }

        public IReadOnlyList<WinwsProcessInfo> FindRunning() => _processes;

        public bool TryStop(WinwsProcessInfo process, out string errorMessage)
        {
            errorMessage = string.Empty;
            return true;
        }
    }
}
