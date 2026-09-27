using ZapretManager.App.Core;

namespace ZapretManager.App.Services;

/// <summary>
/// Автовыбор стратегии в три этапа:
/// 1) проверка без zapret — что реально заблокировано и что не лечится стратегией (DNS);
/// 2) отсев — каждая стратегия проверяется один раз;
/// 3) подтверждение — лучшие стратегии перепроверяются ещё несколько раз вперемешку,
///    чтобы победителя определяли результаты, а не случайный сетевой сбой.
/// </summary>
public sealed class StrategyAutoSelectionService
{
    internal const int FinalistCount = 3;
    internal const int ConfirmationRounds = 2;

    private readonly IZapretRunner _zapretRunner;
    private readonly IStrategyProbe _probe;
    private readonly TimeSpan _settleDelay;
    private readonly IReadOnlyList<StrategyTestTarget> _targets;

    public StrategyAutoSelectionService(
        IZapretRunner zapretRunner,
        IStrategyProbe probe,
        TimeSpan? settleDelay = null,
        IReadOnlyList<StrategyTestTarget>? targets = null)
    {
        _zapretRunner = zapretRunner;
        _probe = probe;
        // winws.exe появляется в списке процессов раньше, чем WinDivert начинает перехватывать трафик.
        _settleDelay = settleDelay ?? TimeSpan.FromSeconds(1);
        _targets = targets?.ToArray() ?? StrategyTestTarget.GetDefaultTargets();
    }

    public async Task<StrategyAutoSelectionResult> RunAsync(
        IReadOnlyList<StrategyInfo> strategies,
        CancellationToken cancellationToken,
        IProgress<StrategyAutoSelectionProgress>? progress = null)
    {
        if (strategies.Count == 0)
        {
            return new StrategyAutoSelectionResult(
                StrategyAutoSelectionStatus.NoStrategies,
                null,
                Array.Empty<StrategyTestSummary>(),
                "Нет доступных general*.bat стратегий для проверки.");
        }

        var initialStop = _zapretRunner.Stop();
        if (initialStop.Status == ZapretRunStatus.CommandFailed)
        {
            return StopFailed(null, Array.Empty<StrategyTestSummary>(), initialStop);
        }

        var current = strategies.FirstOrDefault(strategy => strategy.IsSelected);
        var tracker = new ProgressTracker(
            progress,
            strategies.Count + (strategies.Count > 1 ? Math.Min(FinalistCount, strategies.Count) * ConfirmationRounds : 0));
        tracker.Report(string.Empty, "Подготовка...");
        var baseline = StrategyBaselineReport.From(await _probe.ProbeAsync(_targets, cancellationToken).ConfigureAwait(false));

        // Этап 2: отсев.
        var attemptsByStrategy = new Dictionary<StrategyInfo, List<ProbeAttempt>>();
        var screening = new List<StrategyTestSummary>();
        for (var index = 0; index < strategies.Count; index++)
        {
            var strategy = strategies[index];
            tracker.Report(strategy.FileName, $"Проверяется {index + 1}/{strategies.Count}: {strategy.FileName}");
            var run = await RunStrategyAsync(strategy, cancellationToken).ConfigureAwait(false);
            if (run.StopFailure is not null)
            {
                return StopFailed(strategy, StrategyAutoSelectionAnalyzer.Rank(screening, current), run.StopFailure);
            }

            tracker.Advance();
            if (run.Attempts is null)
            {
                screening.Add(StrategyTestSummary.NotStarted(strategy));
                continue;
            }

            attemptsByStrategy[strategy] = [.. run.Attempts];
            screening.Add(StrategyTestSummary.FromAttempts(strategy, run.Attempts));
        }

        if (attemptsByStrategy.Count == 0)
        {
            return new StrategyAutoSelectionResult(
                StrategyAutoSelectionStatus.StartFailed,
                null,
                screening,
                "Не запустилась ни одна стратегия. Проверьте runtime и журнал.");
        }

        // Этап 3: подтверждение финалистов.
        var finalists = StrategyAutoSelectionAnalyzer.Rank(screening, current)
            .Where(summary => !summary.StartFailed && summary.HttpOk > 0)
            .Take(FinalistCount)
            .Select(summary => summary.Strategy)
            .ToList();
        var rounds = 1;
        if (finalists.Count > 1)
        {
            for (var round = 1; round <= ConfirmationRounds; round++)
            {
                // Сдвиг порядка в каждом раунде компенсирует дрейф сети во времени.
                foreach (var strategy in finalists.Skip(round % finalists.Count).Concat(finalists.Take(round % finalists.Count)))
                {
                    tracker.Report(strategy.FileName, $"Перепроверка {round}/{ConfirmationRounds}: {strategy.FileName}");
                    var run = await RunStrategyAsync(strategy, cancellationToken).ConfigureAwait(false);
                    if (run.StopFailure is not null)
                    {
                        return StopFailed(strategy, StrategyAutoSelectionAnalyzer.Rank(screening, current), run.StopFailure);
                    }

                    tracker.Advance();
                    var attempts = attemptsByStrategy[strategy];
                    attempts.AddRange(run.Attempts ?? FailedCopy(attempts, rounds));
                }

                rounds++;
            }
        }

        var finalistSet = finalists.ToHashSet();
        var summaries = StrategyAutoSelectionAnalyzer
            .Rank(finalists.Select(strategy => StrategyTestSummary.FromAttempts(strategy, attemptsByStrategy[strategy], rounds)), current)
            .Concat(StrategyAutoSelectionAnalyzer.Rank(screening.Where(summary => !finalistSet.Contains(summary.Strategy)), current))
            .ToArray();
        tracker.Complete();

        return BuildResult(summaries, baseline, current);
    }

    private async Task<StrategyRun> RunStrategyAsync(StrategyInfo strategy, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var start = _zapretRunner.Start(strategy);
        if (!start.IsSuccess)
        {
            var stopAfterFailedStart = _zapretRunner.Stop();
            return stopAfterFailedStart.Status == ZapretRunStatus.CommandFailed
                ? new StrategyRun(null, stopAfterFailedStart)
                : new StrategyRun(null, null);
        }

        IReadOnlyList<ProbeAttempt> attempts;
        try
        {
            if (_settleDelay > TimeSpan.Zero)
            {
                await Task.Delay(_settleDelay, cancellationToken).ConfigureAwait(false);
            }

            attempts = await _probe.ProbeAsync(_targets, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            _zapretRunner.Stop();
            throw;
        }

        var stopAfterProbe = _zapretRunner.Stop();
        return stopAfterProbe.Status == ZapretRunStatus.CommandFailed
            ? new StrategyRun(null, stopAfterProbe)
            : new StrategyRun(attempts, null);
    }

    /// <summary>Стратегия не запустилась при перепроверке — все её учитываемые попытки считаются провалом.</summary>
    private static IEnumerable<ProbeAttempt> FailedCopy(IReadOnlyList<ProbeAttempt> attempts, int rounds)
    {
        return attempts
            .Take(attempts.Count / rounds)
            .Select(attempt => attempt.IsCounted ? attempt with { Outcome = ProbeOutcome.Failed, Latency = TimeSpan.Zero } : attempt)
            .ToArray();
    }

    private static StrategyAutoSelectionResult BuildResult(
        IReadOnlyList<StrategyTestSummary> summaries,
        StrategyBaselineReport baseline,
        StrategyInfo? current)
    {
        var notes = BuildNotes(summaries, baseline, current);
        var best = summaries[0];
        if (best.StartFailed || best.HttpOk == 0)
        {
            return new StrategyAutoSelectionResult(
                StrategyAutoSelectionStatus.NoWorkingStrategy,
                null,
                summaries,
                "Ни одна стратегия не открыла проверочные адреса.",
                notes);
        }

        if (baseline.Blocked.Count > 0 && best.SuccessRate <= baseline.SuccessRate)
        {
            return new StrategyAutoSelectionResult(
                StrategyAutoSelectionStatus.NoWorkingStrategy,
                null,
                summaries,
                "Ни одна стратегия не улучшила доступ по сравнению с работой без zapret.",
                notes);
        }

        var message = StrategyAutoSelectionAnalyzer.IsSameStrategy(best.Strategy, current)
            ? $"Текущая стратегия {best.Strategy.DisplayName} среди лучших — оставлена."
            : $"Лучшая стратегия: {best.Strategy.DisplayName}.";
        return new StrategyAutoSelectionResult(
            StrategyAutoSelectionStatus.Completed,
            best.Strategy,
            summaries,
            message,
            notes);
    }

    /// <summary>Короткие наблюдения для подсказки у итога автовыбора: по одной мысли на строку.</summary>
    private static IReadOnlyList<string> BuildNotes(
        IReadOnlyList<StrategyTestSummary> summaries,
        StrategyBaselineReport baseline,
        StrategyInfo? current)
    {
        var notes = new List<string>();
        var checkedCount = baseline.Blocked.Count + baseline.Reachable.Count;
        if (checkedCount > 0)
        {
            notes.Add(baseline.Blocked.Count == 0
                ? "Без zapret открываются все проверочные сайты — стратегии почти не отличаются."
                : $"Без zapret не открываются {baseline.Blocked.Count} из {checkedCount} сайтов: {JoinHosts(baseline.Blocked)}.");
        }

        if (baseline.DnsFailures.Count > 0)
        {
            notes.Add($"Не находятся через DNS: {JoinHosts(baseline.DnsFailures)}. zapret тут не поможет — нужен другой DNS.");
        }

        if (baseline.ResolvedViaFallbackDns.Count > 0)
        {
            notes.Add(
                $"DNS провайдера не находит: {JoinHosts(baseline.ResolvedViaFallbackDns)}. " +
                "Если сайт не открывается в браузере, включите в нём защищённый DNS.");
        }

        if (baseline.CertificateErrors.Count > 0)
        {
            notes.Add($"Подменён сертификат: {JoinHosts(baseline.CertificateErrors)} — похоже на заглушку провайдера.");
        }

        var best = summaries[0];
        var tiedWithBest = summaries
            .Skip(1)
            .Where(summary => summary.Rounds == best.Rounds && StrategyAutoSelectionAnalyzer.HaveSameScore(summary, best))
            .Select(summary => summary.Strategy.DisplayName)
            .ToArray();
        if (tiedWithBest.Length > 0 && !best.StartFailed && best.HttpOk > 0)
        {
            var choice = StrategyAutoSelectionAnalyzer.IsSameStrategy(best.Strategy, current)
                ? "оставлена текущая"
                : "выбрана более быстрая";
            notes.Add(
                $"Одинаковый результат у {best.Strategy.DisplayName} и {string.Join(", ", tiedWithBest)}, {choice}. " +
                "Если что-то не работает — попробуйте другую из них.");
        }

        var notStarted = summaries.Where(summary => summary.StartFailed).Select(summary => summary.Strategy.DisplayName).ToArray();
        if (notStarted.Length > 0)
        {
            notes.Add("Не запустились: " + string.Join(", ", notStarted) + ".");
        }

        return notes;
    }

    private static string JoinHosts(IEnumerable<StrategyTestTarget> targets)
    {
        const int maximumHosts = 3;
        var hosts = targets.Select(target => target.Host).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var shown = string.Join(", ", hosts.Take(maximumHosts));
        return hosts.Length > maximumHosts ? $"{shown} и ещё {hosts.Length - maximumHosts}" : shown;
    }

    private static StrategyAutoSelectionResult StopFailed(
        StrategyInfo? strategy,
        IReadOnlyList<StrategyTestSummary> summaries,
        ZapretRunResult stopResult)
    {
        var context = strategy is null ? "перед автовыбором" : $"во время проверки {strategy.DisplayName}";
        return new StrategyAutoSelectionResult(
            StrategyAutoSelectionStatus.StopFailed,
            null,
            summaries,
            $"Не удалось остановить winws.exe {context}: {stopResult.Message}");
    }

    private sealed record StrategyRun(IReadOnlyList<ProbeAttempt>? Attempts, ZapretRunResult? StopFailure);

    private sealed class ProgressTracker
    {
        private readonly IProgress<StrategyAutoSelectionProgress>? _progress;
        private readonly int _total;
        private int _completed;
        private string _currentName = string.Empty;

        public ProgressTracker(IProgress<StrategyAutoSelectionProgress>? progress, int total)
        {
            _progress = progress;
            _total = total;
        }

        public void Report(string strategyName, string message)
        {
            _currentName = strategyName;
            _progress?.Report(new StrategyAutoSelectionProgress(_completed, _total, strategyName, message));
        }

        public void Advance()
        {
            _completed++;
            _progress?.Report(new StrategyAutoSelectionProgress(_completed, _total, _currentName, $"Готово {_completed}/{_total}"));
        }

        public void Complete()
        {
            if (_completed < _total)
            {
                _completed = _total;
                _progress?.Report(new StrategyAutoSelectionProgress(_completed, _total, _currentName, "Готово"));
            }
        }
    }
}
