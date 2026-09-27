namespace ZapretManager.App.Services;

public interface IStrategyProbe
{
    Task<IReadOnlyList<ProbeAttempt>> ProbeAsync(
        IReadOnlyList<StrategyTestTarget> targets,
        CancellationToken cancellationToken);
}
