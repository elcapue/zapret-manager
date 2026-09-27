using ZapretManager.App.Core;
using ZapretManager.App.Services;

namespace ZapretManager.Tests;

public sealed class StrategyAutoSelectionServiceTests
{
    private static readonly StrategyTestTarget YouTube = Target("YouTube", "https://yt.example", StrategyTargetGroup.YouTube);
    private static readonly StrategyTestTarget Discord = Target("Discord", "https://dc.example", StrategyTargetGroup.Discord);
    private static readonly StrategyTestTarget Other = Target("Other", "https://other.example", StrategyTargetGroup.Baseline);
    private static readonly StrategyTestTarget[] Targets = [YouTube, Discord, Other];

    private static readonly StrategyInfo General = Strategy("general.bat");
    private static readonly StrategyInfo Alt = Strategy("general (ALT).bat");
    private static readonly StrategyInfo Alt2 = Strategy("general (ALT2).bat");

    [Fact]
    public async Task RunAsync_ChecksBaselineThenEachStrategyAndConfirmsFinalists()
    {
        var runner = new FakeZapretRunner();
        var probe = new ScriptedProbe(runner, baseline: Attempts(yt: false, dc: false, other: true));
        probe.Set(General, Attempts(yt: true, dc: false, other: true));
        probe.Set(Alt, Attempts(yt: true, dc: true, other: true));
        var service = CreateService(runner, probe);

        var result = await service.RunAsync([General, Alt], CancellationToken.None);

        Assert.Equal(StrategyAutoSelectionStatus.Completed, result.Status);
        Assert.Equal(Alt, result.BestStrategy);
        Assert.Equal([Alt, General], result.TopStrategies.Select(summary => summary.Strategy));
        Assert.Null(probe.StrategiesProbed[0]);
        Assert.Equal(1 + StrategyAutoSelectionService.ConfirmationRounds, runner.Calls.Count(call => call == "Start:general (ALT).bat"));
        Assert.Equal(1 + StrategyAutoSelectionService.ConfirmationRounds, runner.Calls.Count(call => call == "Start:general.bat"));
        Assert.Equal(["Stop", "Start:general.bat", "Stop", "Start:general (ALT).bat", "Stop"], runner.Calls.Take(5));
        Assert.Equal(1 + StrategyAutoSelectionService.ConfirmationRounds, result.TopStrategies[0].Rounds);
        Assert.Contains(result.Notes, note => note.Contains("не открываются 2 из 3") && note.Contains("yt.example"));
    }

    [Fact]
    public async Task RunAsync_WhenQualityTies_PrefersLowerLatencyInsteadOfName()
    {
        var runner = new FakeZapretRunner();
        var probe = new ScriptedProbe(runner, baseline: Attempts(yt: false, dc: false, other: true));
        probe.Set(General, Attempts(yt: true, dc: true, other: true, latencyMs: 300));
        probe.Set(Alt, Attempts(yt: true, dc: true, other: true, latencyMs: 100));
        var service = CreateService(runner, probe);

        var result = await service.RunAsync([General, Alt], CancellationToken.None);

        Assert.Equal(Alt, result.BestStrategy);
        Assert.Equal(100, result.TopStrategies[0].MedianLatencyMs);
        Assert.Contains(result.Notes, note => note.Contains("Одинаковый результат") && note.Contains("выбрана более быстрая"));
    }

    [Fact]
    public async Task RunAsync_WhenCurrentStrategyTiesForBest_KeepsItDespiteHigherLatency()
    {
        var current = new StrategyInfo("general.bat", "C:/runtime/general.bat", isSelected: true);
        var runner = new FakeZapretRunner();
        var probe = new ScriptedProbe(runner, baseline: Attempts(yt: false, dc: false, other: true));
        probe.Set(current, Attempts(yt: true, dc: true, other: true, latencyMs: 300));
        probe.Set(Alt, Attempts(yt: true, dc: true, other: true, latencyMs: 100));
        var service = CreateService(runner, probe);

        var result = await service.RunAsync([current, Alt], CancellationToken.None);

        Assert.Equal("general.bat", result.BestStrategy?.FileName);
        Assert.Contains("оставлена", result.Message);
        Assert.Contains(result.Notes, note => note.Contains("оставлена текущая"));
    }

    [Fact]
    public async Task RunAsync_WhenCurrentStrategyIsWorse_ReplacesIt()
    {
        var current = new StrategyInfo("general.bat", "C:/runtime/general.bat", isSelected: true);
        var runner = new FakeZapretRunner();
        var probe = new ScriptedProbe(runner, baseline: Attempts(yt: false, dc: false, other: true));
        probe.Set(current, Attempts(yt: true, dc: false, other: true));
        probe.Set(Alt, Attempts(yt: true, dc: true, other: true));
        var service = CreateService(runner, probe);

        var result = await service.RunAsync([current, Alt], CancellationToken.None);

        Assert.Equal(Alt, result.BestStrategy);
        Assert.StartsWith("Лучшая стратегия", result.Message);
    }

    [Fact]
    public async Task RunAsync_ConfirmationRoundsOverrideLuckyScreeningResult()
    {
        var runner = new FakeZapretRunner();
        var probe = new ScriptedProbe(runner, baseline: Attempts(yt: false, dc: false, other: true));
        // general.bat повезло на отсеве, но при перепроверке YouTube и Discord не открываются.
        probe.Set(General,
            Attempts(yt: true, dc: true, other: true),
            Attempts(yt: false, dc: false, other: true),
            Attempts(yt: false, dc: false, other: true));
        probe.Set(Alt,
            Attempts(yt: true, dc: false, other: true),
            Attempts(yt: true, dc: true, other: true),
            Attempts(yt: true, dc: true, other: true));
        var service = CreateService(runner, probe);

        var result = await service.RunAsync([General, Alt], CancellationToken.None);

        Assert.Equal(Alt, result.BestStrategy);
    }

    [Fact]
    public async Task RunAsync_WhenOneStrategyFailsToStart_ContinuesWithOthers()
    {
        var runner = new FakeZapretRunner { FailingStarts = { General.FileName } };
        var probe = new ScriptedProbe(runner, baseline: Attempts(yt: false, dc: false, other: true));
        probe.Set(Alt, Attempts(yt: true, dc: true, other: true));
        var service = CreateService(runner, probe);

        var result = await service.RunAsync([General, Alt], CancellationToken.None);

        Assert.Equal(StrategyAutoSelectionStatus.Completed, result.Status);
        Assert.Equal(Alt, result.BestStrategy);
        Assert.True(result.Summaries.Single(summary => summary.Strategy == General).StartFailed);
        Assert.DoesNotContain(result.TopStrategies, summary => summary.StartFailed);
        Assert.Contains(result.Notes, note => note.Contains("Не запустились: general."));
    }

    [Fact]
    public async Task RunAsync_WhenNoStrategyStarts_ReturnsStartFailed()
    {
        var runner = new FakeZapretRunner { FailingStarts = { General.FileName, Alt.FileName } };
        var service = CreateService(runner, new ScriptedProbe(runner, Attempts(yt: false, dc: false, other: true)));

        var result = await service.RunAsync([General, Alt], CancellationToken.None);

        Assert.Equal(StrategyAutoSelectionStatus.StartFailed, result.Status);
        Assert.Null(result.BestStrategy);
    }

    [Fact]
    public async Task RunAsync_ExcludesDnsFailuresFromScoringAndReportsThem()
    {
        var runner = new FakeZapretRunner();
        var probe = new ScriptedProbe(
            runner,
            baseline: [Attempt(YouTube, ProbeOutcome.DnsFailure), Attempt(Discord, ProbeOutcome.Failed), Attempt(Other, ProbeOutcome.Ok)]);
        probe.Set(General, [Attempt(YouTube, ProbeOutcome.DnsFailure), Attempt(Discord, ProbeOutcome.Ok), Attempt(Other, ProbeOutcome.Ok)]);
        var service = CreateService(runner, probe);

        var result = await service.RunAsync([General], CancellationToken.None);

        var summary = Assert.Single(result.Summaries);
        Assert.Equal(2, summary.Attempts);
        Assert.Equal(1.0, summary.SuccessRate);
        Assert.Contains(result.Notes, note => note.Contains("Не находятся через DNS: yt.example"));
    }

    [Fact]
    public async Task RunAsync_WhenNoStrategyBeatsWorkingWithoutZapret_SelectsNothing()
    {
        var runner = new FakeZapretRunner();
        var probe = new ScriptedProbe(runner, baseline: Attempts(yt: false, dc: false, other: true));
        probe.Set(General, Attempts(yt: false, dc: false, other: true));
        probe.Set(Alt, Attempts(yt: false, dc: false, other: true));
        var service = CreateService(runner, probe);

        var result = await service.RunAsync([General, Alt], CancellationToken.None);

        Assert.Equal(StrategyAutoSelectionStatus.NoWorkingStrategy, result.Status);
        Assert.Null(result.BestStrategy);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task RunAsync_WhenNoStrategies_ReturnsNoStrategies()
    {
        var runner = new FakeZapretRunner();
        var service = CreateService(runner, new ScriptedProbe(runner, []));

        var result = await service.RunAsync(Array.Empty<StrategyInfo>(), CancellationToken.None);

        Assert.Equal(StrategyAutoSelectionStatus.NoStrategies, result.Status);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task RunAsync_ReportsProgressUpToCompletion()
    {
        var runner = new FakeZapretRunner();
        var probe = new ScriptedProbe(runner, baseline: Attempts(yt: false, dc: false, other: true));
        probe.Set(General, Attempts(yt: true, dc: true, other: true));
        probe.Set(Alt, Attempts(yt: true, dc: false, other: true));
        probe.Set(Alt2, Attempts(yt: false, dc: false, other: true));
        var progress = new RecordingProgress();
        var service = CreateService(runner, probe);

        await service.RunAsync([General, Alt, Alt2], CancellationToken.None, progress);

        var total = progress.Events[0].TotalStrategies;
        Assert.Equal(3 + 3 * StrategyAutoSelectionService.ConfirmationRounds, total);
        Assert.Contains(progress.Events, item => item.CompletedStrategies == 1 && item.CurrentStrategyName == "general.bat");
        Assert.Equal(total, progress.Events[^1].CompletedStrategies);
        Assert.True(progress.Events.Select(item => item.CompletedStrategies).SequenceEqual(progress.Events.Select(item => item.CompletedStrategies).Order()));
    }

    [Fact]
    public async Task RunAsync_WhenCancelledDuringProbe_StopsCurrentStrategyAndThrows()
    {
        var runner = new FakeZapretRunner();
        using var cancellation = new CancellationTokenSource();
        var probe = new ScriptedProbe(runner, Attempts(yt: false, dc: false, other: true))
        {
            OnStrategyProbe = cancellation.Cancel
        };
        var service = CreateService(runner, probe);

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.RunAsync([General], cancellation.Token));

        Assert.Equal(["Stop", "Start:general.bat", "Stop"], runner.Calls);
    }

    [Fact]
    public async Task RunAsync_WhenInitialStopFails_DoesNotStartAnything()
    {
        var runner = new FakeZapretRunner
        {
            StopResult = new ZapretRunResult(ZapretRunStatus.CommandFailed, "Access denied")
        };
        var service = CreateService(runner, new ScriptedProbe(runner, []));

        var result = await service.RunAsync([General], CancellationToken.None);

        Assert.Equal(StrategyAutoSelectionStatus.StopFailed, result.Status);
        Assert.Equal(["Stop"], runner.Calls);
    }

    private static StrategyAutoSelectionService CreateService(FakeZapretRunner runner, IStrategyProbe probe)
    {
        return new StrategyAutoSelectionService(runner, probe, settleDelay: TimeSpan.Zero, targets: Targets);
    }

    private static StrategyInfo Strategy(string fileName) => new(fileName, "C:/runtime/" + fileName, false);

    private static StrategyTestTarget Target(string name, string url, StrategyTargetGroup group)
    {
        return StrategyTestTarget.TryCreate(name, url, group)!;
    }

    private static ProbeAttempt Attempt(StrategyTestTarget target, ProbeOutcome outcome, int latencyMs = 100)
    {
        return new ProbeAttempt(target, outcome, TimeSpan.FromMilliseconds(outcome == ProbeOutcome.Ok ? latencyMs : 0));
    }

    private static ProbeAttempt[] Attempts(bool yt, bool dc, bool other, int latencyMs = 100)
    {
        return
        [
            Attempt(YouTube, yt ? ProbeOutcome.Ok : ProbeOutcome.Failed, latencyMs),
            Attempt(Discord, dc ? ProbeOutcome.Ok : ProbeOutcome.Failed, latencyMs),
            Attempt(Other, other ? ProbeOutcome.Ok : ProbeOutcome.Failed, latencyMs)
        ];
    }

    private sealed class RecordingProgress : IProgress<StrategyAutoSelectionProgress>
    {
        public List<StrategyAutoSelectionProgress> Events { get; } = [];

        public void Report(StrategyAutoSelectionProgress value) => Events.Add(value);
    }

    private sealed class FakeZapretRunner : IZapretRunner
    {
        public List<string> Calls { get; } = [];
        public HashSet<string> FailingStarts { get; } = new(StringComparer.OrdinalIgnoreCase);
        public ZapretRunResult StopResult { get; set; } = new(ZapretRunStatus.Stopped, "stopped");
        public StrategyInfo? Running { get; private set; }

        public ZapretRunResult Start(StrategyInfo? strategy)
        {
            Calls.Add("Start:" + strategy?.FileName);
            if (strategy is null || FailingStarts.Contains(strategy.FileName))
            {
                return new ZapretRunResult(ZapretRunStatus.CommandFailed, "failed");
            }

            Running = strategy;
            return new ZapretRunResult(ZapretRunStatus.Started, "started");
        }

        public ZapretRunResult Stop()
        {
            Calls.Add("Stop");
            Running = null;
            return StopResult;
        }

        public ZapretRunResult Restart(StrategyInfo? strategy) => throw new NotSupportedException();
    }

    /// <summary>Отдаёт заранее заданные результаты в зависимости от запущенной стратегии и номера её проверки.</summary>
    private sealed class ScriptedProbe : IStrategyProbe
    {
        private readonly FakeZapretRunner _runner;
        private readonly IReadOnlyList<ProbeAttempt> _baseline;
        private readonly Dictionary<StrategyInfo, IReadOnlyList<ProbeAttempt>[]> _results = [];
        private readonly Dictionary<StrategyInfo, int> _probeCounts = [];

        public ScriptedProbe(FakeZapretRunner runner, IReadOnlyList<ProbeAttempt> baseline)
        {
            _runner = runner;
            _baseline = baseline;
        }

        public List<StrategyInfo?> StrategiesProbed { get; } = [];

        public Action? OnStrategyProbe { get; init; }

        public void Set(StrategyInfo strategy, params IReadOnlyList<ProbeAttempt>[] runs) => _results[strategy] = runs;

        public Task<IReadOnlyList<ProbeAttempt>> ProbeAsync(IReadOnlyList<StrategyTestTarget> targets, CancellationToken cancellationToken)
        {
            var strategy = _runner.Running;
            StrategiesProbed.Add(strategy);
            if (strategy is null)
            {
                return Task.FromResult(_baseline);
            }

            OnStrategyProbe?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            var index = _probeCounts.GetValueOrDefault(strategy);
            _probeCounts[strategy] = index + 1;
            var runs = _results.GetValueOrDefault(strategy) ?? [_baseline];
            return Task.FromResult(runs[Math.Min(index, runs.Length - 1)]);
        }
    }
}
