using ZapretManager.App.Core;
using ZapretManager.App.Infrastructure;
using ZapretManager.App.Services;

namespace ZapretManager.Tests;

public sealed class BatStrategyRunnerTests
{
    [Fact]
    public void Start_WhenNoStrategySelected_ReturnsFailureWithoutRunningCommand()
    {
        var runner = new FakeCommandRunner();
        var zapretRunner = new BatStrategyRunner(runner, new AppConfig(), new FakeWinwsProcessInspector());

        var result = zapretRunner.Start(null);

        Assert.Equal(ZapretRunStatus.NoStrategySelected, result.Status);
        Assert.Empty(runner.Calls);
        Assert.Empty(runner.DetachedCalls);
    }

    [Fact]
    public void Start_WhenExternalWinwsIsRunning_ReturnsAlreadyRunningWithoutStartingAnotherProcess()
    {
        var runtimeDirectory = Directory.CreateTempSubdirectory("zapret-runtime-test-").FullName;
        var inspector = new FakeWinwsProcessInspector();
        inspector.Processes.Add(new WinwsProcessInfo(1234, DateTime.UtcNow, Path.Combine(runtimeDirectory, "bin", "winws.exe")));
        var runner = new FakeCommandRunner();
        var zapretRunner = new BatStrategyRunner(runner, new AppConfig(), inspector, TimeSpan.Zero);

        var result = zapretRunner.Start(CreateStrategy(runtimeDirectory));

        Assert.Equal(ZapretRunStatus.AlreadyRunningNotOwned, result.Status);
        Assert.Contains("исходный launcher", result.Message);
        Assert.Contains("не останавливает чужие процессы", result.Message);
        Assert.Empty(runner.DetachedCalls);
    }

    [Fact]
    public void Start_WhenManagerProcessIsStillRunning_DoesNotStartSecondInstance()
    {
        var runtimeDirectory = Directory.CreateTempSubdirectory("zapret-runtime-test-").FullName;
        var strategy = CreateStrategy(runtimeDirectory);
        var process = new WinwsProcessInfo(1234, DateTime.UtcNow, Path.Combine(runtimeDirectory, "bin", "winws.exe"));
        var inspector = new FakeWinwsProcessInspector();
        inspector.Processes.Add(process);
        var config = CreateConfigFor(process, strategy.FileName);
        var runner = new FakeCommandRunner();
        var zapretRunner = new BatStrategyRunner(runner, config, inspector, TimeSpan.Zero);

        var result = zapretRunner.Start(strategy);

        Assert.Equal(ZapretRunStatus.AlreadyRunningManaged, result.Status);
        Assert.Empty(runner.DetachedCalls);
    }

    [Fact]
    public void Start_WhenStrategyLaunchesWinws_TracksExactProcessAndUsesBackgroundWrapper()
    {
        var runtimeDirectory = Directory.CreateTempSubdirectory("zapret-runtime-test-").FullName;
        var strategy = CreateStrategy(runtimeDirectory);
        var inspector = new FakeWinwsProcessInspector();
        var runner = new FakeCommandRunner();
        runner.OnStartDetached = () => inspector.Processes.Add(new WinwsProcessInfo(
            4321,
            DateTime.UtcNow,
            Path.Combine(runtimeDirectory, "bin", "winws.exe")));
        var config = new AppConfig();
        var zapretRunner = new BatStrategyRunner(runner, config, inspector, TimeSpan.Zero);

        var result = zapretRunner.Start(strategy);

        Assert.Equal(ZapretRunStatus.Started, result.Status);
        Assert.Equal(4321, config.ManagedProcess?.ProcessId);
        Assert.Equal(strategy.FileName, config.ManagedProcess?.StrategyFileName);
        Assert.Contains(runner.DetachedCalls, call =>
            call.FileName == "cmd.exe" &&
            call.Arguments == "/d /c zapret-manager-run.cmd" &&
            call.WorkingDirectory == runtimeDirectory);

        var launchScript = File.ReadAllText(Path.Combine(runtimeDirectory, "zapret-manager-run.cmd"));
        Assert.StartsWith("@set NO_UPDATE_CHECK=1\r\n@echo off", launchScript);
        Assert.Contains("start \"zapret: %~n0\" /b \"%BIN%winws.exe\"", launchScript);
        Assert.DoesNotContain(" /min ", launchScript);
    }

    [Fact]
    public void WriteLaunchScript_PreservesOriginalBytesAndDropsBom()
    {
        var runtimeDirectory = Directory.CreateTempSubdirectory("zapret-runtime-test-").FullName;
        var strategyPath = Path.Combine(runtimeDirectory, "general.bat");
        // UTF-8 BOM + кириллица в UTF-8 + байт 0xAE (кириллица в OEM 866) — ничего не должно перекодироваться.
        var body = new System.Text.UTF8Encoding(false).GetBytes(
            "@echo off\r\n:: Комментарий\r\nstart \"zapret: %~n0\" /min \"%BIN%winws.exe\" --wf-tcp=443\r\n");
        File.WriteAllBytes(strategyPath, [0xEF, 0xBB, 0xBF, .. body, 0xAE]);

        BatStrategyRunner.WriteLaunchScript(runtimeDirectory, strategyPath);

        var written = File.ReadAllBytes(Path.Combine(runtimeDirectory, BatStrategyRunner.LaunchScriptFileName));
        var expectedBody = new System.Text.UTF8Encoding(false).GetBytes(
            "@set NO_UPDATE_CHECK=1\r\n@echo off\r\n:: Комментарий\r\nstart \"zapret: %~n0\" /b \"%BIN%winws.exe\" --wf-tcp=443\r\n");
        Assert.Equal([.. expectedBody, 0xAE], written);
    }

    [Fact]
    public void Start_WhenWinwsDoesNotAppear_DoesNotPersistManagedState()
    {
        var runtimeDirectory = Directory.CreateTempSubdirectory("zapret-runtime-test-").FullName;
        var strategy = CreateStrategy(runtimeDirectory);
        var config = new AppConfig();
        var zapretRunner = new BatStrategyRunner(new FakeCommandRunner(), config, new FakeWinwsProcessInspector(), TimeSpan.Zero);

        var result = zapretRunner.Start(strategy);

        Assert.Equal(ZapretRunStatus.CommandFailed, result.Status);
        Assert.Null(config.ManagedProcess);
        Assert.Contains("Не удалось подтвердить запуск", result.Message);
    }

    [Fact]
    public void Stop_WhenManagedProcessIdentityDoesNotMatch_DoesNotStopExternalWinws()
    {
        var inspector = new FakeWinwsProcessInspector();
        inspector.Processes.Add(new WinwsProcessInfo(777, DateTime.UtcNow, "C:/external/bin/winws.exe"));
        var config = new AppConfig
        {
            ManagedProcess = new ManagedProcessState
            {
                ProcessId = 777,
                StartedAtUtc = DateTime.UtcNow.AddMinutes(-1),
                ExecutablePath = "C:/runtime/bin/winws.exe",
                StrategyFileName = "general.bat"
            }
        };
        var zapretRunner = new BatStrategyRunner(new FakeCommandRunner(), config, inspector, TimeSpan.Zero);

        var result = zapretRunner.Stop();

        Assert.Equal(ZapretRunStatus.NotStartedByManager, result.Status);
        Assert.True(result.IsSuccess);
        Assert.Equal("Запущенный процесс не найден", result.Message);
        Assert.Empty(inspector.StoppedProcessIds);
        Assert.Null(config.ManagedProcess);
        Assert.Single(inspector.Processes);
    }

    [Fact]
    public void Stop_WhenManagerStartedExactProcess_StopsOnlyItsPidAndClearsState()
    {
        var process = new WinwsProcessInfo(1234, DateTime.UtcNow, "C:/runtime/bin/winws.exe");
        var inspector = new FakeWinwsProcessInspector();
        inspector.Processes.Add(process);
        inspector.Processes.Add(new WinwsProcessInfo(5678, DateTime.UtcNow, "C:/external/bin/winws.exe"));
        var config = CreateConfigFor(process, "general.bat");
        var zapretRunner = new BatStrategyRunner(new FakeCommandRunner(), config, inspector, TimeSpan.Zero);

        var result = zapretRunner.Stop();

        Assert.Equal(ZapretRunStatus.Stopped, result.Status);
        Assert.Equal(new[] { process.ProcessId }, inspector.StoppedProcessIds);
        Assert.Null(config.ManagedProcess);
        Assert.Single(inspector.Processes);
        Assert.Equal(5678, inspector.Processes.Single().ProcessId);
    }

    private static StrategyInfo CreateStrategy(string runtimeDirectory)
    {
        Directory.CreateDirectory(Path.Combine(runtimeDirectory, "bin"));
        File.WriteAllText(Path.Combine(runtimeDirectory, "bin", "winws.exe"), string.Empty);
        var strategyPath = Path.Combine(runtimeDirectory, "general (ALT).bat");
        File.WriteAllText(
            strategyPath,
            "@echo off" + Environment.NewLine +
            "start \"zapret: %~n0\" /min \"%BIN%winws.exe\" --wf-tcp=80 ^" + Environment.NewLine +
            "--filter-tcp=443" + Environment.NewLine);
        return new StrategyInfo("general (ALT).bat", strategyPath, isSelected: true);
    }

    private static AppConfig CreateConfigFor(WinwsProcessInfo process, string strategyFileName)
    {
        return new AppConfig
        {
            ManagedProcess = new ManagedProcessState
            {
                ProcessId = process.ProcessId,
                StartedAtUtc = process.StartedAtUtc,
                ExecutablePath = process.ExecutablePath,
                StrategyFileName = strategyFileName
            }
        };
    }

    private sealed class FakeWinwsProcessInspector : IWinwsProcessInspector
    {
        public List<WinwsProcessInfo> Processes { get; } = [];
        public List<int> StoppedProcessIds { get; } = [];

        public IReadOnlyList<WinwsProcessInfo> FindRunning() => Processes.ToArray();

        public bool TryStop(WinwsProcessInfo process, out string errorMessage)
        {
            StoppedProcessIds.Add(process.ProcessId);
            Processes.RemoveAll(candidate => candidate.ProcessId == process.ProcessId && candidate.StartedAtUtc == process.StartedAtUtc);
            errorMessage = string.Empty;
            return true;
        }
    }

    private sealed class FakeCommandRunner : ICommandRunner
    {
        public List<(string FileName, string Arguments, string? WorkingDirectory)> Calls { get; } = [];
        public List<(string FileName, string Arguments, string? WorkingDirectory)> DetachedCalls { get; } = [];
        public Action? OnStartDetached { get; set; }

        public CommandResult Run(string fileName, string arguments, TimeSpan timeout, string? workingDirectory = null)
        {
            Calls.Add((fileName, arguments, workingDirectory));
            return new CommandResult(1, string.Empty, "Unexpected command.");
        }

        public CommandResult StartDetached(string fileName, string arguments, string? workingDirectory = null, bool createNoWindow = true)
        {
            DetachedCalls.Add((fileName, arguments, workingDirectory));
            OnStartDetached?.Invoke();
            return new CommandResult(0, string.Empty, string.Empty);
        }
    }
}
