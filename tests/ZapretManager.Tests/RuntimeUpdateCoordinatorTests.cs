using ZapretManager.App.Services;

namespace ZapretManager.Tests;

public sealed class RuntimeUpdateCoordinatorTests
{
    [Fact]
    public async Task ApplyAsync_WhenZapretStopped_AppliesWithoutTouchingProcess()
    {
        var calls = new List<ZapretActionKind>();
        var coordinator = Create(managedRunning: false, runtimeProcesses: false, calls, Applied());

        var outcome = await coordinator.ApplyAsync();

        Assert.True(outcome.IsSuccess);
        Assert.Equal("updated", outcome.Message);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task ApplyAsync_WhenManagedRunning_StopsAppliesAndStartsAgain()
    {
        var calls = new List<ZapretActionKind>();
        var coordinator = Create(managedRunning: true, runtimeProcesses: true, calls, Applied());

        var outcome = await coordinator.ApplyAsync();

        Assert.True(outcome.IsSuccess);
        Assert.Equal([ZapretActionKind.Stop, ZapretActionKind.Start], calls);
        Assert.Equal("updated\nПерезапуск zapret: ok", outcome.Message);
    }

    [Fact]
    public async Task ApplyAsync_WhenExternalWinwsUsesRuntime_RefusesToUpdate()
    {
        var calls = new List<ZapretActionKind>();
        var applied = false;
        var coordinator = new RuntimeUpdateCoordinator(
            () => false,
            () => true,
            action =>
            {
                calls.Add(action);
                return Task.FromResult(Ok());
            },
            () =>
            {
                applied = true;
                return new UpdateApplyResult(UpdateApplyStatus.Applied, "updated");
            });

        var outcome = await coordinator.ApplyAsync();

        Assert.False(outcome.IsSuccess);
        Assert.Contains("внешний winws.exe", outcome.Message);
        Assert.False(applied);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task ApplyAsync_WhenStopFails_DoesNotApply()
    {
        var applied = false;
        var coordinator = new RuntimeUpdateCoordinator(
            () => true,
            () => true,
            _ => Task.FromResult(new ZapretActionResponse(ZapretActionOutcome.Failed, "busy")),
            () =>
            {
                applied = true;
                return new UpdateApplyResult(UpdateApplyStatus.Applied, "updated");
            });

        var outcome = await coordinator.ApplyAsync();

        Assert.False(outcome.IsSuccess);
        Assert.Contains("busy", outcome.Message);
        Assert.False(applied);
    }

    [Fact]
    public async Task ApplyAsync_WhenApplyThrows_StillRestoresRunningZapret()
    {
        var calls = new List<ZapretActionKind>();
        var coordinator = Create(
            managedRunning: true,
            runtimeProcesses: true,
            calls,
            () => throw new IOException("disk"));

        await Assert.ThrowsAsync<IOException>(coordinator.ApplyAsync);

        Assert.Equal([ZapretActionKind.Stop, ZapretActionKind.Start], calls);
    }

    [Fact]
    public async Task ApplyAsync_WhenApplyFails_ReportsRestoreResult()
    {
        var calls = new List<ZapretActionKind>();
        var coordinator = Create(
            managedRunning: true,
            runtimeProcesses: true,
            calls,
            () => new UpdateApplyResult(UpdateApplyStatus.Failed, "rollback"));

        var outcome = await coordinator.ApplyAsync();

        Assert.False(outcome.IsSuccess);
        Assert.Equal("rollback\n\nВосстановление состояния zapret: ok", outcome.Message);
    }

    private static RuntimeUpdateCoordinator Create(
        bool managedRunning,
        bool runtimeProcesses,
        List<ZapretActionKind> calls,
        Func<UpdateApplyResult> apply)
    {
        return new RuntimeUpdateCoordinator(
            () => managedRunning,
            () => runtimeProcesses,
            action =>
            {
                calls.Add(action);
                return Task.FromResult(Ok());
            },
            apply);
    }

    private static Func<UpdateApplyResult> Applied()
    {
        return () => new UpdateApplyResult(UpdateApplyStatus.Applied, "updated");
    }

    private static ZapretActionResponse Ok()
    {
        return new ZapretActionResponse(ZapretActionOutcome.Succeeded, "ok");
    }
}
