using ZapretManager.App.Core;
using ZapretManager.App.Infrastructure;
using ZapretManager.App.Services;

namespace ZapretManager.Tests;

public sealed class ExistingZapretStopServiceTests
{
    [Fact]
    public void StopExisting_WhenZapretServiceRunning_StopsService()
    {
        var runner = new FakeCommandRunner();
        runner.Set("sc.exe", "stop zapret", new CommandResult(0, "STOP_PENDING", string.Empty));
        var service = CreateService(runner, new FakeWinwsProcessInspector());

        var result = service.StopExisting(new ZapretStatus { ZapretServiceRunning = true });

        Assert.Equal(ExistingZapretStopStatus.Stopped, result.Status);
        Assert.Contains(runner.Calls, call => call.FileName == "sc.exe" && call.Arguments == "stop zapret");
    }

    [Fact]
    public void StopExisting_WhenWinwsRunsFromCurrentRuntime_StopsOnlyThatPid()
    {
        var inspector = new FakeWinwsProcessInspector();
        inspector.Processes.Add(new WinwsProcessInfo(100, DateTime.UtcNow, "C:/runtime/bin/winws.exe"));
        inspector.Processes.Add(new WinwsProcessInfo(200, DateTime.UtcNow, "C:/external/bin/winws.exe"));
        var service = CreateService(new FakeCommandRunner(), inspector);

        var result = service.StopExisting(new ZapretStatus { WinwsProcessRunning = true });

        Assert.Equal(ExistingZapretStopStatus.Stopped, result.Status);
        Assert.Equal(new[] { 100 }, inspector.StoppedProcessIds);
        Assert.Single(inspector.Processes);
        Assert.Equal(200, inspector.Processes.Single().ProcessId);
    }

    [Fact]
    public void StopExisting_WhenOnlyExternalRuntimeExists_LeavesItUntouched()
    {
        var inspector = new FakeWinwsProcessInspector();
        inspector.Processes.Add(new WinwsProcessInfo(200, DateTime.UtcNow, "C:/external/bin/winws.exe"));
        var service = CreateService(new FakeCommandRunner(), inspector);

        var result = service.StopExisting(new ZapretStatus { WinwsProcessRunning = true });

        Assert.Equal(ExistingZapretStopStatus.NothingToStop, result.Status);
        Assert.Empty(inspector.StoppedProcessIds);
        Assert.Single(inspector.Processes);
    }

    [Fact]
    public void StopExisting_WhenCurrentRuntimeProcessCannotBeStopped_ReturnsCommandFailed()
    {
        var inspector = new FakeWinwsProcessInspector { StopError = "Access denied" };
        inspector.Processes.Add(new WinwsProcessInfo(100, DateTime.UtcNow, "C:/runtime/bin/winws.exe"));
        var service = CreateService(new FakeCommandRunner(), inspector);

        var result = service.StopExisting(new ZapretStatus { WinwsProcessRunning = true });

        Assert.Equal(ExistingZapretStopStatus.CommandFailed, result.Status);
        Assert.Contains("Access denied", result.Message);
    }

    private static ExistingZapretStopService CreateService(FakeCommandRunner runner, FakeWinwsProcessInspector inspector)
    {
        return new ExistingZapretStopService(runner, inspector, "C:/runtime");
    }

    private sealed class FakeWinwsProcessInspector : IWinwsProcessInspector
    {
        public List<WinwsProcessInfo> Processes { get; } = [];
        public List<int> StoppedProcessIds { get; } = [];
        public string? StopError { get; init; }

        public IReadOnlyList<WinwsProcessInfo> FindRunning() => Processes.ToArray();

        public bool TryStop(WinwsProcessInfo process, out string errorMessage)
        {
            if (StopError is not null)
            {
                errorMessage = StopError;
                return false;
            }

            StoppedProcessIds.Add(process.ProcessId);
            Processes.RemoveAll(candidate => candidate.ProcessId == process.ProcessId);
            errorMessage = string.Empty;
            return true;
        }
    }

    private sealed class FakeCommandRunner : ICommandRunner
    {
        private readonly Dictionary<string, CommandResult> _results = new(StringComparer.OrdinalIgnoreCase);

        public List<(string FileName, string Arguments)> Calls { get; } = [];

        public void Set(string fileName, string arguments, CommandResult result)
        {
            _results[$"{fileName} {arguments}"] = result;
        }

        public CommandResult Run(string fileName, string arguments, TimeSpan timeout, string? workingDirectory = null)
        {
            Calls.Add((fileName, arguments));
            return _results.TryGetValue($"{fileName} {arguments}", out var result)
                ? result
                : new CommandResult(0, string.Empty, string.Empty);
        }
    }
}
