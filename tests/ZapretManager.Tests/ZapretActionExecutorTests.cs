using ZapretManager.App.Core;
using ZapretManager.App.Infrastructure;
using ZapretManager.App.Services;

namespace ZapretManager.Tests;

public sealed class ZapretActionExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_Start_RunsSelectedStrategy()
    {
        var fixture = new Fixture();
        fixture.WriteStrategy("general.bat");
        fixture.Config.SelectedStrategy = "general.bat";

        var response = await fixture.Executor.ExecuteAsync(ZapretActionKind.Start);

        Assert.Equal(ZapretActionOutcome.Succeeded, response.Outcome);
        Assert.Equal(["Start:general.bat"], fixture.Runner.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_Start_WhenRunnerFails_ReturnsFailed()
    {
        var fixture = new Fixture();
        var response = await fixture.Executor.ExecuteAsync(ZapretActionKind.Start);

        Assert.Equal(ZapretActionOutcome.Failed, response.Outcome);
        Assert.Equal(["Start:"], fixture.Runner.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownAction_ReturnsFailed()
    {
        var fixture = new Fixture();
        var response = await fixture.Executor.ExecuteAsync((ZapretActionKind)999);

        Assert.Equal(ZapretActionOutcome.Failed, response.Outcome);
        Assert.Contains("Неизвестное действие", response.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRunnerThrows_ReturnsFailedInsteadOfThrowing()
    {
        var fixture = new Fixture();
        fixture.WriteStrategy("general.bat");
        fixture.Config.SelectedStrategy = "general.bat";
        fixture.Runner.StartException = new InvalidOperationException("boom");

        var response = await fixture.Executor.ExecuteAsync(ZapretActionKind.Start);

        Assert.Equal(ZapretActionOutcome.Failed, response.Outcome);
        Assert.Contains("boom", response.Message);
    }

    [Fact]
    public async Task ExecuteAsync_StopExisting_WhenStopped_ClearsManagedProcess()
    {
        var fixture = new Fixture();
        fixture.Config.ManagedProcess = new ManagedProcessState
        {
            ProcessId = 100,
            StartedAtUtc = DateTime.UtcNow,
            ExecutablePath = "C:/runtime/bin/winws.exe",
            StrategyFileName = "general.bat"
        };
        fixture.Status = new ZapretStatus { ZapretServiceExists = true, ZapretServiceRunning = true };
        fixture.CommandRunner.Set("sc.exe", "stop zapret", new CommandResult(0, "STOP_PENDING", string.Empty));

        var response = await fixture.Executor.ExecuteAsync(ZapretActionKind.StopExisting);

        Assert.Equal(ZapretActionOutcome.Succeeded, response.Outcome);
        Assert.Null(fixture.Config.ManagedProcess);
    }

    [Fact]
    public async Task ExecuteAsync_AutoSelect_WhenExternalWinwsPresent_RefusesToRun()
    {
        var fixture = new Fixture();
        fixture.WriteStrategy("general.bat");
        fixture.Inspector.Processes.Add(new WinwsProcessInfo(200, DateTime.UtcNow, "C:/other/bin/winws.exe"));

        var response = await fixture.Executor.ExecuteAsync(ZapretActionKind.AutoSelect);

        Assert.Equal(ZapretActionOutcome.Failed, response.Outcome);
        Assert.Contains("внешний winws.exe", response.Message);
        Assert.Empty(fixture.Runner.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_AutoSelect_WhenCancelled_RestoresPreviouslyRunningStrategy()
    {
        var fixture = new Fixture();
        fixture.WriteStrategy("general.bat");
        fixture.Config.SelectedStrategy = "general.bat";
        var startedAt = DateTime.UtcNow;
        fixture.Config.ManagedProcess = new ManagedProcessState
        {
            ProcessId = 100,
            StartedAtUtc = startedAt,
            ExecutablePath = "C:/runtime/bin/winws.exe",
            StrategyFileName = "general.bat"
        };
        fixture.Inspector.Processes.Add(new WinwsProcessInfo(100, startedAt, "C:/runtime/bin/winws.exe"));

        using var cancellation = new CancellationTokenSource();
        fixture.Probe.OnProbe = () =>
        {
            // Отменяем на первой же проверке стратегии (после baseline).
            if (fixture.Runner.Running is not null)
            {
                cancellation.Cancel();
            }
        };

        var response = await fixture.Executor.ExecuteAsync(ZapretActionKind.AutoSelect, cancellationToken: cancellation.Token);

        Assert.Equal(ZapretActionOutcome.Cancelled, response.Outcome);
        Assert.Contains("остановлен", response.Message);
        Assert.Contains("zapret снова включён", response.Message);
        Assert.Equal("Start:general.bat", fixture.Runner.Calls[^1]);
    }

    [Fact]
    public async Task ExecuteAsync_AutoSelect_WhenBetterStrategyFound_SelectsAndPersistsIt()
    {
        var fixture = new Fixture();
        fixture.WriteStrategy("general.bat");
        fixture.WriteStrategy("general (ALT).bat");
        fixture.Config.SelectedStrategy = "general.bat";

        var response = await fixture.Executor.ExecuteAsync(ZapretActionKind.AutoSelect);

        Assert.Equal(ZapretActionOutcome.Succeeded, response.Outcome);
        Assert.Equal("general (ALT).bat", fixture.Config.SelectedStrategy);
        Assert.NotNull(fixture.Config.LastStrategyScan);
        var reloaded = new ConfigService(fixture.Layout.ConfigPath).LoadOrCreate();
        Assert.Equal("general (ALT).bat", reloaded.SelectedStrategy);
        Assert.NotNull(reloaded.LastStrategyScan);
    }

    private sealed class Fixture
    {
        private readonly System.IO.DirectoryInfo _tempDirectory;
        private ScriptedProbe? _probe;

        public Fixture()
        {
            _tempDirectory = Directory.CreateTempSubdirectory("zapret-executor-test-");
            Layout = RuntimeLayout.ForDirectory(_tempDirectory.FullName);
            Layout.EnsureDirectories();
            var configService = new ConfigService(Layout.ConfigPath);
            Config = configService.LoadOrCreate();
            Executor = new ZapretActionExecutor(
                configService,
                Layout,
                Config,
                CommandRunner,
                Inspector,
                runnerFactory: () => Runner,
                detectStatus: () => Status,
                probeFactory: () => Probe,
                settleDelay: TimeSpan.Zero);
        }

        public RuntimeLayout Layout { get; }

        public AppConfig Config { get; }

        public ZapretActionExecutor Executor { get; }

        public FakeZapretRunner Runner { get; } = new();

        public FakeWinwsProcessInspector Inspector { get; } = new();

        public FakeCommandRunner CommandRunner { get; } = new();

        public ScriptedProbe Probe => _probe ??= new ScriptedProbe(Runner);

        public ZapretStatus Status { get; set; } = new();

        public void WriteStrategy(string fileName)
        {
            File.WriteAllText(Path.Combine(Layout.RuntimeDirectory, fileName), "@echo off");
        }
    }

    private sealed class FakeZapretRunner : IZapretRunner
    {
        public List<string> Calls { get; } = [];

        public Exception? StartException { get; set; }

        public StrategyInfo? Running { get; private set; }

        public ZapretRunResult Start(StrategyInfo? strategy)
        {
            Calls.Add("Start:" + strategy?.FileName);
            if (StartException is not null)
            {
                throw StartException;
            }

            if (strategy is null)
            {
                return new ZapretRunResult(ZapretRunStatus.NoStrategySelected, "no strategy");
            }

            Running = strategy;
            return new ZapretRunResult(ZapretRunStatus.Started, "started");
        }

        public ZapretRunResult Stop()
        {
            Calls.Add("Stop");
            Running = null;
            return new ZapretRunResult(ZapretRunStatus.Stopped, "stopped");
        }

        public ZapretRunResult Restart(StrategyInfo? strategy) => throw new NotSupportedException();
    }

    /// <summary>Без zapret всё заблокировано; единственная рабочая стратегия — general (ALT).bat.</summary>
    private sealed class ScriptedProbe : IStrategyProbe
    {
        private const string WorkingStrategyFileName = "general (ALT).bat";

        private readonly FakeZapretRunner _runner;

        public ScriptedProbe(FakeZapretRunner runner)
        {
            _runner = runner;
        }

        public Action? OnProbe { get; set; }

        public Task<IReadOnlyList<ProbeAttempt>> ProbeAsync(
            IReadOnlyList<StrategyTestTarget> targets,
            CancellationToken cancellationToken)
        {
            OnProbe?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            var works = _runner.Running?.FileName == WorkingStrategyFileName;
            IReadOnlyList<ProbeAttempt> attempts = targets
                .Select(target => new ProbeAttempt(
                    target,
                    works ? ProbeOutcome.Ok : ProbeOutcome.Failed,
                    TimeSpan.FromMilliseconds(100)))
                .ToArray();
            return Task.FromResult(attempts);
        }
    }

    private sealed class FakeWinwsProcessInspector : IWinwsProcessInspector
    {
        public List<WinwsProcessInfo> Processes { get; } = [];

        public IReadOnlyList<WinwsProcessInfo> FindRunning() => Processes.ToArray();

        public bool TryStop(WinwsProcessInfo process, out string errorMessage)
        {
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
            _results[fileName + " " + arguments] = result;
        }

        public CommandResult Run(string fileName, string arguments, TimeSpan timeout, string? workingDirectory = null)
        {
            Calls.Add((fileName, arguments));
            return _results.GetValueOrDefault(fileName + " " + arguments, new CommandResult(0, string.Empty, string.Empty));
        }
    }
}
