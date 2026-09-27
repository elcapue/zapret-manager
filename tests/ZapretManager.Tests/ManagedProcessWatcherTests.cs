using System.Diagnostics;
using ZapretManager.App.Core;
using ZapretManager.App.Services;

namespace ZapretManager.Tests;

public sealed class ManagedProcessWatcherTests
{
    [Fact]
    public async Task WaitForExitAsync_CompletesWhenWatchedProcessExits()
    {
        using var process = StartSleeper(seconds: 1);
        var state = new ManagedProcessState
        {
            ProcessId = process.Id,
            StartedAtUtc = process.StartTime.ToUniversalTime()
        };

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var exited = await ManagedProcessWatcher.WaitForExitAsync(state, timeout.Token);

        Assert.True(exited);
        Assert.True(process.HasExited);
    }

    [Fact]
    public async Task WaitForAnyExitAsync_CompletesWhenFirstOfSeveralProcessesExits()
    {
        using var shortLived = StartSleeper(seconds: 1);
        using var longLived = StartSleeper(seconds: 30);
        try
        {
            var processes = new[]
            {
                new WinwsProcessInfo(longLived.Id, longLived.StartTime.ToUniversalTime(), string.Empty),
                new WinwsProcessInfo(shortLived.Id, shortLived.StartTime.ToUniversalTime(), string.Empty)
            };

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var exited = await ManagedProcessWatcher.WaitForAnyExitAsync(processes, timeout.Token);

            Assert.True(exited);
            Assert.True(shortLived.HasExited);
            Assert.False(longLived.HasExited);
        }
        finally
        {
            longLived.Kill();
        }
    }

    [Fact]
    public async Task WaitForExitAsync_WhenPidBelongsToAnotherProcess_ReportsExit()
    {
        using var process = StartSleeper(seconds: 30);
        try
        {
            var state = new ManagedProcessState
            {
                ProcessId = process.Id,
                StartedAtUtc = process.StartTime.ToUniversalTime().AddMinutes(-5)
            };

            var exited = await ManagedProcessWatcher.WaitForExitAsync(state, CancellationToken.None);

            Assert.True(exited);
        }
        finally
        {
            process.Kill();
        }
    }

    [Fact]
    public async Task WaitForExitAsync_CanBeCancelled()
    {
        using var process = StartSleeper(seconds: 30);
        try
        {
            var state = new ManagedProcessState
            {
                ProcessId = process.Id,
                StartedAtUtc = process.StartTime.ToUniversalTime()
            };
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => ManagedProcessWatcher.WaitForExitAsync(state, cancellation.Token));
        }
        finally
        {
            process.Kill();
        }
    }

    private static Process StartSleeper(int seconds)
    {
        return Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -Command Start-Sleep -Seconds {seconds}")
        {
            CreateNoWindow = true,
            UseShellExecute = false
        })!;
    }
}
