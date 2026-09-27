using ZapretManager.App.Services;

namespace ZapretManager.Tests;

public sealed class ManagedProcessShutdownServiceTests
{
    [Fact]
    public async Task StopBeforeExit_WhenNoManagedProcess_ExitsWithoutStopRequest()
    {
        var stopRequests = 0;
        var service = new ManagedProcessShutdownService(
            isManagedProcessRunning: () => false,
            stopManagedProcess: () =>
            {
                stopRequests++;
                return Task.FromResult(Succeeded());
            });

        var result = await service.StopBeforeExitAsync();

        Assert.True(result.CanExit);
        Assert.Equal(0, stopRequests);
    }

    [Fact]
    public async Task StopBeforeExit_WhenManagedProcessStops_AllowsExit()
    {
        var isRunning = true;
        var service = new ManagedProcessShutdownService(
            isManagedProcessRunning: () => isRunning,
            stopManagedProcess: () =>
            {
                isRunning = false;
                return Task.FromResult(Succeeded());
            });

        var result = await service.StopBeforeExitAsync();

        Assert.True(result.CanExit);
    }

    [Fact]
    public async Task StopBeforeExit_WhenStopIsCancelledAndProcessStillRuns_BlocksExit()
    {
        var service = new ManagedProcessShutdownService(
            isManagedProcessRunning: () => true,
            stopManagedProcess: () => Task.FromResult(new ZapretActionResponse(
                ZapretActionOutcome.Cancelled,
                "Остановка отменена.")));

        var result = await service.StopBeforeExitAsync();

        Assert.False(result.CanExit);
        Assert.Equal("Остановка отменена.", result.Message);
    }

    [Fact]
    public async Task StopBeforeExit_WhenProcessExitsDuringFailedRequest_AllowsExit()
    {
        var checks = 0;
        var service = new ManagedProcessShutdownService(
            isManagedProcessRunning: () => ++checks == 1,
            stopManagedProcess: () => Task.FromResult(new ZapretActionResponse(
                ZapretActionOutcome.Failed,
                "Процесс уже завершён.")));

        var result = await service.StopBeforeExitAsync();

        Assert.True(result.CanExit);
    }

    private static ZapretActionResponse Succeeded()
    {
        return new ZapretActionResponse(ZapretActionOutcome.Succeeded, "zapret остановлен.");
    }
}
